# Delivery Checklist

This checklist defines the minimum backend gate before handing the project to another developer, a reviewer, or the Unity frontend integration pass.

## Required Checks

Run from the `OperatorsDraft` root:

```bash
./doctor.sh
```

Expected result:

- `run_evoflow.sh --help: ok`
- `OperatorRunner` builds with `0 Warning(s), 0 Error(s)`
- unit tests pass
- render contract validation passes for `exports/test3.json`

## Secrets

No API keys or local credentials should be committed. Dynamic LLM-backed runs must read the key from:

```bash
export DASHSCOPE_API_KEY="..."
```

## Backend Service Smoke Test

Start the local service:

```bash
./run_backend_server.sh
```

Check these endpoints:

```bash
curl -s http://127.0.0.1:8000/api/health
curl -s http://127.0.0.1:8000/api/datasets
curl -s 'http://127.0.0.1:8000/api/render/test3?limit=1000&includeSelectedIds=false'
curl -s 'http://127.0.0.1:8000/api/render/test3?summary=true'
```

## Version-Control Hygiene

The repository should not track generated dependency caches or build outputs:

- `.python_deps/`
- `.dotnet/TelemetryStorageService/`
- `Library/`
- `OperatorRunner/bin/`
- `OperatorRunner/obj/`
- `__pycache__/`

If any of those appear as tracked changes, remove them from the git index without deleting local files:

```bash
git ls-files -ci --exclude-standard -z | xargs -0 git rm --cached -r
```

## Known Remaining Delivery Risks

- Dynamic LLM-backed workflow execution still depends on external service availability and timeout behavior.
- Unity Editor and standalone build rendering must be validated in the target desktop environment.
- Standalone packaging must keep `OperatorsDraft` next to the Unity app or bundle it under `StreamingAssets/EvoFlowBackend`.
