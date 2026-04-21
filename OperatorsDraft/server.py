#!/usr/bin/env python3
"""Local EvoFlow backend service for the Unity desktop frontend.

The service is intentionally implemented with Python's standard library so it
can run in this project folder without installing extra web framework
dependencies. It exposes both the raw EvoFlow workflow JSON and a Unity-ready
render JSON adapted to the existing frontend schema.
"""

from __future__ import annotations

import argparse
import json
import subprocess
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any, Dict, List
from urllib.parse import urlparse

ROOT = Path(__file__).resolve().parent
EXPORTS_DIR = ROOT / "exports"
DEFAULT_WORKFLOW_ID = "test3"


def load_json(path: Path) -> Dict[str, Any]:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def resolve_workflow_path(workflow_id: str) -> Path:
    safe_id = workflow_id.strip().replace("/", "").replace("..", "")
    if not safe_id or safe_id == "latest":
        safe_id = DEFAULT_WORKFLOW_ID

    path = EXPORTS_DIR / f"{safe_id}.json"
    if not path.exists():
        raise FileNotFoundError(f"Workflow export '{safe_id}' was not found in {EXPORTS_DIR}.")

    return path


def resolve_dataset_path(dataset: str) -> Path:
    value = (dataset or "").strip()
    candidates = []
    if value:
        raw_path = Path(value).expanduser()
        candidates.append(raw_path)
        candidates.append(ROOT / value)
        candidates.append(ROOT / "demo_data" / value)

    candidates.append(ROOT / "demo_data" / "hurricane_sandy_2012_100k_sample.csv")

    for candidate in candidates:
        if candidate.exists():
            return candidate.resolve()

    raise FileNotFoundError(f"Dataset '{dataset}' was not found.")


def list_datasets() -> Dict[str, Any]:
    datasets = []
    data_dir = ROOT / "demo_data"
    if data_dir.exists():
        for csv_path in sorted(data_dir.glob("*.csv")):
            row_count = 0
            try:
                with csv_path.open("r", encoding="utf-8", errors="ignore") as handle:
                    row_count = max(0, sum(1 for _ in handle) - 1)
            except OSError:
                row_count = 0

            datasets.append(
                {
                    "id": csv_path.name,
                    "label": csv_path.stem.replace("_", " "),
                    "path": str(csv_path),
                    "relativePath": f"demo_data/{csv_path.name}",
                    "rowCount": row_count,
                }
            )

    return {"status": "success", "datasets": datasets}


def error_payload(stage: str, message: str, details: str = "") -> Dict[str, Any]:
    return {
        "status": "failed",
        "error": {
            "stage": stage,
            "message": message,
            "details": details,
        },
    }


def make_task_id(task: str) -> str:
    base = "".join(ch.lower() if ch.isalnum() else "_" for ch in (task or "task"))
    base = "_".join(part for part in base.split("_") if part)[:40] or "task"
    return f"unity_{base}_{int(time.time())}"


def execute_evoflow_task(body: Dict[str, Any]) -> Dict[str, Any]:
    task = str(body.get("task") or body.get("rawText") or "Find concentrated morning pickup hotspots.").strip()
    dataset = resolve_dataset_path(str(body.get("dataset") or body.get("dataPath") or ""))
    task_id = str(body.get("taskId") or body.get("task_id") or make_task_id(task))
    export_path = EXPORTS_DIR / f"{task_id}.json"

    population = int(body.get("population") or 6)
    generations = int(body.get("generations") or 3)
    elite_size = int(body.get("eliteSize") or body.get("elite_size") or 2)

    command = [
        str(ROOT / "run_evoflow.sh"),
        "--task",
        task,
        "--data-path",
        str(dataset),
        "--export-json",
        str(export_path),
        "--task-id",
        task_id,
        "--population",
        str(population),
        "--generations",
        str(generations),
        "--elite-size",
        str(elite_size),
    ]

    completed = subprocess.run(
        command,
        cwd=str(ROOT),
        check=True,
        capture_output=True,
        text=True,
        timeout=int(body.get("timeoutSeconds") or 180),
    )

    result = load_json(export_path)
    result.setdefault("serverExecution", {})
    result["serverExecution"].update(
        {
            "executed": True,
            "taskId": task_id,
            "exportPath": str(export_path),
            "stdoutPreview": completed.stdout[-4000:],
            "stderrPreview": completed.stderr[-2000:],
        }
    )
    return result


def workflow_from_run_body(body: Dict[str, Any]) -> Dict[str, Any]:
    should_execute = bool(body.get("execute") or body.get("runEvoFlow") or body.get("run_evoflow"))
    if should_execute:
        return execute_evoflow_task(body)

    workflow_id = str(body.get("workflowId") or body.get("workflow_id") or DEFAULT_WORKFLOW_ID)
    return load_json(resolve_workflow_path(workflow_id))


def as_float(value: Any, fallback: float = 0.0) -> float:
    try:
        if value is None:
            return fallback
        return float(value)
    except (TypeError, ValueError):
        return fallback


def as_int(value: Any, fallback: int = 0) -> int:
    try:
        if value is None:
            return fallback
        return int(value)
    except (TypeError, ValueError):
        return fallback


def adapt_to_unity_backend_result(evoflow_result: Dict[str, Any]) -> Dict[str, Any]:
    render_plan = evoflow_result.get("visualization", {}).get("renderPlan", {})
    primary_view = render_plan.get("primaryView", {})
    geometry = render_plan.get("geometry", {})
    selection = render_plan.get("selection", {})
    result_summary = evoflow_result.get("resultSummary", {})
    selected_workflow = evoflow_result.get("selectedWorkflow", {})
    task = evoflow_result.get("task", {})

    selected_ids = (
        result_summary.get("selectedRowIds")
        or selection.get("selectedRowIds")
        or selection.get("selectedRowSample")
        or []
    )
    selected_set = {str(item) for item in selected_ids}

    points: List[Dict[str, Any]] = []
    for point in geometry.get("points", []):
        position = point.get("position", {})
        row_id = str(point.get("rowId", point.get("pointId", "")))
        points.append(
            {
                "id": row_id,
                "x": as_float(position.get("x")),
                "y": as_float(position.get("y")),
                "z": as_float(position.get("z")),
                "time": as_float(point.get("timeValue")),
                "isSelected": bool(point.get("selected", False)) or row_id in selected_set,
            }
        )

    links: List[Dict[str, Any]] = []
    for link in geometry.get("links", []):
        links.append(
            {
                "originIndex": as_int(link.get("originIndex")),
                "destinationIndex": as_int(link.get("destinationIndex")),
            }
        )

    operators = [{"name": str(name)} for name in selected_workflow.get("operators", [])]
    scores = selected_workflow.get("scores", {})
    view_type = str(primary_view.get("type") or result_summary.get("viewType") or "Point")
    view_name = str(primary_view.get("name") or "EvoFlowView")
    include_links = bool(links) and view_type.strip().upper() in {"STC", "LINK", "LINKS"}
    selected_count = as_int(result_summary.get("selectedPointCount"), len(selected_set))

    return {
        "meta": {"schemaVersion": "2.0.0-unity-backend-service"},
        "task": {
            "rawTaskText": str(task.get("rawText") or "EvoFlow workflow result"),
        },
        "selectedWorkflow": {
            "operators": operators,
            "scores": {
                "executionScore": as_float(scores.get("execution", scores.get("executionScore"))),
                "llmScore": as_float(scores.get("llm", scores.get("llmScore"))),
                "fitness": as_float(scores.get("fitness")),
            },
        },
        "visualizationPayload": {
            "views": [
                {
                    "viewType": view_type,
                    "viewName": view_name,
                    "projectionPlane": "XY",
                    "visible": True,
                    "includeLinks": include_links,
                    "pointSizeScale": 1.0,
                    "points": points,
                    "links": links,
                    "encodingState": {
                        "selectedCount": selected_count,
                        "highlightMode": str(
                            evoflow_result.get("visualization", {})
                            .get("intent", {})
                            .get("targetRole", "All")
                        ),
                    },
                }
            ]
        },
        "resultSummary": {
            "selectedRowIds": selected_ids,
            "selectedPointCount": selected_count,
            "backendBuilt": bool(result_summary.get("backendBuilt", primary_view.get("backendReady", False))),
        },
    }


class EvoFlowRequestHandler(BaseHTTPRequestHandler):
    server_version = "EvoFlowLocalBackend/0.1"

    def do_OPTIONS(self) -> None:
        self.send_response(204)
        self.send_cors_headers()
        self.end_headers()

    def do_GET(self) -> None:
        parsed = urlparse(self.path)
        path = parsed.path.rstrip("/") or "/"

        try:
            if path in {"/", "/health", "/api", "/api/health"}:
                self.write_json({"status": "ok", "service": "evoflow-local-backend"})
                return

            if path in {"/datasets", "/api/datasets"}:
                self.write_json(list_datasets())
                return

            if path.startswith("/workflow/") or path.startswith("/api/workflow/"):
                workflow_id = path.split("/")[-1]
                self.write_json(load_json(resolve_workflow_path(workflow_id)))
                return

            if path.startswith("/render/") or path.startswith("/api/render/"):
                workflow_id = path.split("/")[-1]
                workflow = load_json(resolve_workflow_path(workflow_id))
                self.write_json(adapt_to_unity_backend_result(workflow))
                return

            self.write_json(error_payload("routing", f"Unknown endpoint: {path}"), status=404)
        except Exception as exc:
            self.write_json(error_payload("get_request", str(exc)), status=500)

    def do_POST(self) -> None:
        parsed = urlparse(self.path)
        path = parsed.path.rstrip("/") or "/"

        try:
            body = self.read_json_body()

            if path in {"/workflow/run", "/api/workflow/run"}:
                self.write_json(workflow_from_run_body(body))
                return

            if path in {"/render/run", "/api/render/run"}:
                workflow = workflow_from_run_body(body)
                self.write_json(adapt_to_unity_backend_result(workflow))
                return

            self.write_json(error_payload("routing", f"Unknown endpoint: {path}"), status=404)
        except subprocess.TimeoutExpired as exc:
            self.write_json(error_payload("evoflow_timeout", "EvoFlow execution timed out.", str(exc)), status=504)
        except subprocess.CalledProcessError as exc:
            stdout_preview = exc.stdout[-2000:] if exc.stdout else ""
            stderr_preview = exc.stderr[-4000:] if exc.stderr else ""
            details = f"stdout:\n{stdout_preview}\nstderr:\n{stderr_preview}"
            self.write_json(error_payload("evoflow_execution", str(exc), details), status=500)
        except Exception as exc:
            self.write_json(error_payload("post_request", str(exc)), status=500)

    def read_json_body(self) -> Dict[str, Any]:
        length = int(self.headers.get("Content-Length", "0") or "0")
        if length <= 0:
            return {}

        raw = self.rfile.read(length).decode("utf-8")
        if not raw.strip():
            return {}

        value = json.loads(raw)
        if not isinstance(value, dict):
            raise ValueError("Request body must be a JSON object.")

        return value

    def log_message(self, fmt: str, *args: Any) -> None:
        print("[EvoFlowBackend] " + fmt % args)

    def send_cors_headers(self) -> None:
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type")

    def write_json(self, payload: Dict[str, Any], status: int = 200) -> None:
        raw = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(raw)))
        self.send_cors_headers()
        self.end_headers()
        self.wfile.write(raw)


def main() -> None:
    parser = argparse.ArgumentParser(description="Run the local EvoFlow backend service for Unity.")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", default=8000, type=int)
    args = parser.parse_args()

    server = ThreadingHTTPServer((args.host, args.port), EvoFlowRequestHandler)
    print(f"EvoFlow local backend running at http://{args.host}:{args.port}")
    print("Available endpoints:")
    print("  GET /api/health")
    print("  GET /api/datasets")
    print("  GET /api/workflow/test3")
    print("  GET /api/render/test3")
    print("  POST /api/workflow/run")
    print("  POST /api/render/run")
    print("    body: { workflowId, task, dataset, execute=false }")
    server.serve_forever()


if __name__ == "__main__":
    main()
