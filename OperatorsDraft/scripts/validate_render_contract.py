#!/usr/bin/env python3
"""Validate the Unity backend render contract produced from an EvoFlow export."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))

import server  # noqa: E402


class ContractError(ValueError):
    pass


def require_mapping(value: Any, path: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise ContractError(f"{path} must be an object.")
    return value


def require_list(value: Any, path: str) -> list[Any]:
    if not isinstance(value, list):
        raise ContractError(f"{path} must be an array.")
    return value


def require_keys(mapping: dict[str, Any], path: str, keys: list[str]) -> None:
    missing = [key for key in keys if key not in mapping]
    if missing:
        raise ContractError(f"{path} is missing required key(s): {', '.join(missing)}.")


def validate_render_contract(payload: dict[str, Any]) -> None:
    require_keys(payload, "$", ["meta", "task", "selectedWorkflow", "visualizationPayload", "resultSummary"])

    meta = require_mapping(payload["meta"], "$.meta")
    schema_version = meta.get("schemaVersion")
    if not isinstance(schema_version, str) or not schema_version:
        raise ContractError("$.meta.schemaVersion must be a non-empty string.")

    workflow = require_mapping(payload["selectedWorkflow"], "$.selectedWorkflow")
    require_list(workflow.get("operators"), "$.selectedWorkflow.operators")
    require_mapping(workflow.get("scores"), "$.selectedWorkflow.scores")

    visualization = require_mapping(payload["visualizationPayload"], "$.visualizationPayload")
    views = require_list(visualization.get("views"), "$.visualizationPayload.views")
    if not views:
        raise ContractError("$.visualizationPayload.views must contain at least one view.")

    for index, raw_view in enumerate(views):
        view_path = f"$.visualizationPayload.views[{index}]"
        view = require_mapping(raw_view, view_path)
        require_keys(
            view,
            view_path,
            [
                "viewType",
                "viewName",
                "projectionPlane",
                "visible",
                "includeLinks",
                "pointSizeScale",
                "points",
                "links",
                "encodingState",
            ],
        )
        require_list(view["points"], f"{view_path}.points")
        require_list(view["links"], f"{view_path}.links")
        encoding_state = require_mapping(view["encodingState"], f"{view_path}.encodingState")
        require_keys(encoding_state, f"{view_path}.encodingState", ["selectedCount"])

    result_summary = require_mapping(payload["resultSummary"], "$.resultSummary")
    require_keys(
        result_summary,
        "$.resultSummary",
        ["selectedRowIds", "selectedPointCount", "backendBuilt"],
    )
    require_list(result_summary["selectedRowIds"], "$.resultSummary.selectedRowIds")


def main() -> int:
    parser = argparse.ArgumentParser(description="Validate a Unity backend render contract.")
    parser.add_argument("export_json", type=Path, help="Raw EvoFlow export JSON to adapt and validate.")
    parser.add_argument("--limit", type=int, default=None, help="Optional point limit for adapted render payload.")
    parser.add_argument("--summary", action="store_true", help="Validate summary-only adapted payload.")
    parser.add_argument(
        "--no-selected-ids",
        action="store_true",
        help="Omit selectedRowIds from the adapted payload before validation.",
    )
    args = parser.parse_args()

    try:
        export_payload = server.load_json(args.export_json)
        render_payload = server.adapt_to_unity_backend_result(
            export_payload,
            point_limit=args.limit,
            include_selected_ids=not args.no_selected_ids,
            summary_only=args.summary,
        )
        validate_render_contract(render_payload)
    except Exception as exc:
        print(f"render contract validation failed: {exc}", file=sys.stderr)
        return 1

    print(f"render contract validation ok: {args.export_json}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
