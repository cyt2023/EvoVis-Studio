# 2026-04-23 Delivery Hardening

## Summary

This update shifts `OperatorsDraft` from a research-demo backend toward a handoff-ready engineering project. The work focuses on repository hygiene, repeatable validation, render-contract checks, and removing machine-specific or secret-bearing assumptions.

## Changes

- Added `.gitignore` coverage for local dependency caches, Python bytecode, .NET build outputs, telemetry files, logs, and generated runtime exports.
- Removed previously tracked generated files from the git index while keeping local files available for development.
- Added `requirements.txt` so Python dependencies can be installed reproducibly into `.python_deps`.
- Added `doctor.sh` as the backend delivery gate.
- Added `scripts/validate_render_contract.py` to validate Unity-facing backend render payloads.
- Added `tests/test_server_contract.py` with service contract tests for dataset listing, summary render output, point-limited render output, and query option parsing.
- Added `DELIVERY_CHECKLIST.md` with handoff checks, service smoke-test commands, version-control hygiene guidance, and known remaining delivery risks.
- Updated `server.py` so `/api/render/<id>` and `/api/render/run` can return smaller payloads through `limit`, `summary`, `includeSelectedIds`, and `includeLinks`.
- Added render metadata fields: `totalPointCount`, `returnedPointCount`, and `truncated`.
- Cleaned C# nullable annotations across the operator model and backend adapter path. `OperatorRunner` now builds with zero warnings.
- Removed hard-coded DashScope API keys from `evoflow/real_llm.py` and `evoflow/test.py`; dynamic LLM calls now read `DASHSCOPE_API_KEY`.
- Replaced local absolute paths in README, docs, and sample export JSON with repository-relative paths.

## Verification

Ran:

```bash
./doctor.sh
```

Result:

```text
run_evoflow.sh --help: ok
Build succeeded.
0 Warning(s)
0 Error(s)
Ran 4 tests
OK
render contract validation ok
Doctor check completed.
```

Also scanned the backend repo for local absolute paths and hard-coded key-like strings outside ignored/generated directories.

## Remaining Delivery Risks

- Dynamic LLM-backed workflow execution still depends on external DashScope availability and timeout behavior.
- Unity Editor and standalone build rendering still need validation in the target desktop environment.
- Standalone packaging must preserve the expected `OperatorsDraft` location next to the Unity build or bundle it under `StreamingAssets/EvoFlowBackend`.
- Any previously exposed DashScope key should be rotated in the provider console.
