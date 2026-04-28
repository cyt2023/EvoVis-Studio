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
import os
import subprocess
import sys
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any, Dict, List
from urllib.parse import parse_qs, urlparse

ROOT = Path(sys.executable).resolve().parent if getattr(sys, "frozen", False) else Path(__file__).resolve().parent
EXPORTS_DIR = ROOT / "exports"
DEFAULT_WORKFLOW_ID = "test3"
FROZEN_RUNNER_EXE = ROOT / "EvoFlowRunner.exe"


def load_local_env() -> None:
    for env_path in (ROOT.parent / ".env", ROOT.parent / ".env.local", ROOT / ".env", ROOT / ".env.local"):
        if not env_path.exists():
            continue

        with env_path.open("r", encoding="utf-8") as handle:
            for line in handle:
                stripped = line.strip()
                if not stripped or stripped.startswith("#") or "=" not in stripped:
                    continue

                key, value = stripped.split("=", 1)
                key = key.strip()
                value = value.strip().strip('"').strip("'")
                if key and key not in os.environ:
                    os.environ[key] = value


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


def llm_status() -> Dict[str, Any]:
    load_local_env()
    has_api_key = bool(os.environ.get("DASHSCOPE_API_KEY", "").strip())
    model = os.environ.get("DASHSCOPE_MODEL", "qwen-turbo").strip() or "qwen-turbo"
    ready = has_api_key
    missing = []
    if not has_api_key:
        missing.append("DASHSCOPE_API_KEY")

    return {
        "ready": ready,
        "provider": "dashscope-compatible",
        "model": model,
        "endpoint": "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions",
        "apiKeyConfigured": has_api_key,
        "missing": missing,
    }


def require_llm_ready() -> None:
    status = llm_status()
    if status["ready"]:
        return

    raise RuntimeError(
        "Real LLM is required but not ready. Missing: "
        + ", ".join(status["missing"])
        + ". Set DASHSCOPE_API_KEY."
    )


def make_task_id(task: str) -> str:
    base = "".join(ch.lower() if ch.isalnum() else "_" for ch in (task or "task"))
    base = "_".join(part for part in base.split("_") if part)[:40] or "task"
    return f"unity_{base}_{int(time.time())}"


def execute_evoflow_task(body: Dict[str, Any]) -> Dict[str, Any]:
    require_llm = bool(body.get("requireLlm") or body.get("require_llm"))
    if require_llm:
        require_llm_ready()

    task = str(body.get("task") or body.get("rawText") or "Find concentrated morning pickup hotspots.").strip()
    dataset = resolve_dataset_path(str(body.get("dataset") or body.get("dataPath") or ""))
    task_id = str(body.get("taskId") or body.get("task_id") or make_task_id(task))
    export_path = EXPORTS_DIR / f"{task_id}.json"

    population = int(body.get("population") or 6)
    generations = int(body.get("generations") or 3)
    elite_size = int(body.get("eliteSize") or body.get("elite_size") or 2)

    if FROZEN_RUNNER_EXE.exists():
        command = [str(FROZEN_RUNNER_EXE)]
    elif getattr(sys, "frozen", False):
        raise FileNotFoundError(f"Packaged EvoFlow runner was not found: {FROZEN_RUNNER_EXE}")
    else:
        command = [sys.executable, str(ROOT / "evoflow" / "operator_search_main.py")]

    command.extend(
        [
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
    )
    if require_llm:
        command.append("--require-llm")

    env = os.environ.copy()
    python_path = str(ROOT / ".python_deps")
    env["PYTHONPATH"] = python_path + (os.pathsep + env["PYTHONPATH"] if env.get("PYTHONPATH") else "")
    env["HOME"] = str(ROOT)
    env["DOTNET_CLI_HOME"] = str(ROOT)
    env["PATH"] = str(ROOT / ".dotnet") + os.pathsep + env.get("PATH", "")

    completed = subprocess.run(
        command,
        cwd=str(ROOT),
        check=True,
        capture_output=True,
        text=True,
        env=env,
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


def as_bool(value: Any, fallback: bool = False) -> bool:
    if value is None:
        return fallback
    if isinstance(value, bool):
        return value
    text = str(value).strip().lower()
    if text in {"1", "true", "yes", "y", "on"}:
        return True
    if text in {"0", "false", "no", "n", "off"}:
        return False
    return fallback


def first_query_value(query: Dict[str, List[str]], name: str, default: str = "") -> str:
    values = query.get(name)
    if not values:
        return default
    return values[0]


def adapt_to_unity_backend_result(
    evoflow_result: Dict[str, Any],
    *,
    point_limit: int | None = None,
    include_selected_ids: bool = True,
    include_links: bool = True,
    summary_only: bool = False,
    requested_view_type: str | None = None,
) -> Dict[str, Any]:
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

    raw_points = geometry.get("points", [])
    total_point_count = len(raw_points) if isinstance(raw_points, list) else 0
    if summary_only:
        point_source = []
    elif point_limit is not None and point_limit >= 0:
        point_source = raw_points[:point_limit]
    else:
        point_source = raw_points

    points: List[Dict[str, Any]] = []
    for point in point_source:
        position = point.get("position", {})
        row_id = str(point.get("rowId", point.get("pointId", "")))
        points.append(
            {
                "id": row_id,
                "x": as_float(position.get("x")),
                "y": as_float(position.get("y")),
                "z": as_float(position.get("z")),
                "time": as_float(point.get("timeValue")),
                "colorValue": as_float(point.get("colorValue")),
                "sizeValue": as_float(point.get("sizeValue")),
                "isSelected": bool(point.get("selected", False)) or row_id in selected_set,
            }
        )

    returned_point_count = len(points)
    raw_links = geometry.get("links", []) if include_links and not summary_only else []
    links: List[Dict[str, Any]] = []
    max_returned_links = max(0, returned_point_count // 2)
    for link in raw_links:
        if len(links) >= max_returned_links:
            break

        origin_index = as_int(link.get("originIndex"))
        destination_index = as_int(link.get("destinationIndex"))
        if origin_index < 0 or destination_index < 0:
            continue
        if origin_index >= returned_point_count or destination_index >= returned_point_count:
            continue

        links.append(
            {
                "originIndex": origin_index,
                "destinationIndex": destination_index,
            }
        )

    operators = [{"name": str(name)} for name in selected_workflow.get("operators", [])]
    scores = selected_workflow.get("scores", {})
    view_type = str(primary_view.get("type") or result_summary.get("viewType") or "Point")
    if requested_view_type and requested_view_type.strip().lower() not in {"auto", "default"}:
        view_type = requested_view_type.strip()
    view_name = str(primary_view.get("name") or "EvoFlowView")
    include_links = bool(links) and view_type.strip().upper() in {"STC", "LINK", "LINKS"}
    selected_count = as_int(result_summary.get("selectedPointCount"), len(selected_set))
    returned_selected_ids = selected_ids if include_selected_ids else []

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
                        "totalPointCount": total_point_count,
                        "returnedPointCount": len(points),
                        "truncated": len(points) < total_point_count,
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
            "selectedRowIds": returned_selected_ids,
            "selectedPointCount": selected_count,
            "totalPointCount": total_point_count,
            "returnedPointCount": len(points),
            "truncated": len(points) < total_point_count,
            "backendBuilt": bool(result_summary.get("backendBuilt", primary_view.get("backendReady", False))),
        },
    }


def render_options_from_query(query: Dict[str, List[str]]) -> Dict[str, Any]:
    limit_text = first_query_value(query, "limit", "").strip()
    point_limit = None
    if limit_text:
        point_limit = max(0, int(limit_text))

    return {
        "point_limit": point_limit,
        "include_selected_ids": as_bool(first_query_value(query, "includeSelectedIds", "true"), True),
        "include_links": as_bool(first_query_value(query, "includeLinks", "true"), True),
        "summary_only": as_bool(first_query_value(query, "summary", "false"), False),
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
        query = parse_qs(parsed.query)

        try:
            if path in {"/", "/health", "/api", "/api/health"}:
                self.write_json({"status": "ok", "service": "evoflow-local-backend", "llm": llm_status()})
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
                self.write_json(adapt_to_unity_backend_result(workflow, **render_options_from_query(query)))
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
                options = {
                    "point_limit": int(body["limit"]) if body.get("limit") is not None else None,
                    "include_selected_ids": as_bool(body.get("includeSelectedIds"), True),
                    "include_links": as_bool(body.get("includeLinks"), True),
                    "summary_only": as_bool(body.get("summary"), False),
                    "requested_view_type": str(body.get("viewType") or body.get("view_type") or "Auto"),
                }
                self.write_json(adapt_to_unity_backend_result(workflow, **options))
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
    print("  GET /api/render/test3?limit=1000&includeSelectedIds=false")
    print("  POST /api/workflow/run")
    print("  POST /api/render/run")
    print("    body: { workflowId, task, dataset, execute=false }")
    server.serve_forever()


if __name__ == "__main__":
    main()
