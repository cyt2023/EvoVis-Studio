# EvoVis Studio Backend

`EvoVis Studio Backend` is the EvoFlow-side runtime for **natural-language-driven visualization workflow search**.

It takes a user task in plain language, infers dataset structure, uses EvoFlow-style search to compose a C# operator workflow, executes that workflow through a .NET runner, evaluates the result with LLM-assisted scoring, and exports a Unity-facing JSON artifact.

## EvoFlow Paper

This project is inspired by the EvoFlow paper:

- EvoFlow: Evolving Diverse Agentic Workflows On The Fly
- arXiv abstract: https://arxiv.org/abs/2502.07373
- PDF: https://arxiv.org/pdf/2502.07373.pdf

## What This Project Does

Current end-to-end pipeline:

`natural language task -> dataset schema inference -> task spec generation -> workflow search -> C# operator execution -> evaluation -> Unity-ready JSON export`

In practice, the system can already:

- read a new CSV and infer likely id/time/spatial/value columns
- parse a natural-language visualization request
- search over a pool of C# visualization operators
- execute the selected workflow through `OperatorRunner`
- score the result using both runner-side structured evaluation and LLM evaluation
- export a final JSON contract for future Unity consumption

## Current Scope

This repository is **not** yet a full Unity application.

Right now it is focused on the backend side of the project:

- planner: understand the task and dataset
- executor: run the selected operator workflow
- exporter: emit a stable JSON artifact for a future Unity frontend

The Unity side is intentionally postponed. The current goal is to make the EvoFlow/backend side robust enough that Unity can later act as a thin consumer of the exported result.

## Main Components

### `evoflow/`
Python orchestration layer for:

- dataset schema inference
- task parsing
- EvoFlow-style workflow search
- LLM-based workflow evaluation
- Unity JSON export

Main entry point:

- [operator_search_main.py](evoflow/operator_search_main.py)

### `OperatorRunner/`
.NET execution layer that:

- receives a normalized workflow request
- executes C# operators in sequence
- returns execution results, diagnostics, self-evaluation, and visualization payloads

### Operator Packages

- `operators/Core/`
- `operators/Data/`
- `operators/View/`
- `operators/Query/`
- `operators/Filter/`
- `operators/Backend/`

These contain the C# operator implementations used by the workflow search.

### `demo_data/`
Current example datasets:

- [taxi_od_small.csv](demo_data/taxi_od_small.csv)
- [first_week_of_may_2011_10k_sample.csv](demo_data/first_week_of_may_2011_10k_sample.csv)
- [hurricane_sandy_2012_100k_sample.csv](demo_data/hurricane_sandy_2012_100k_sample.csv)

### `exports/`
Example exported JSON artifacts:

- [test3.json](exports/test3.json)
- [test3_schema_sample.json](exports/test3_schema_sample.json)
- [hurricane_sandy_unity_export.json](exports/hurricane_sandy_unity_export.json)

## Quick Start

Install Python dependencies if `.python_deps/` is not already present in your local workspace:

```bash
python3 -m pip install -r requirements.txt -t ./.python_deps
```

Dynamic LLM-backed runs require a DashScope API key:

```bash
export DASHSCOPE_API_KEY="..."
```

Use the one-command launcher:

```bash
./run_evoflow.sh \
  --task "Find concentrated morning pickup hotspots in the Hurricane Sandy sample and render them as a backend-ready point visualization." \
  --data-path ./demo_data/hurricane_sandy_2012_100k_sample.csv \
  --population 1 \
  --generations 0 \
  --elite-size 1 \
  --export-json ./exports/test3.json \
  --task-id test3
```

You can also inspect the CLI help:

```bash
./run_evoflow.sh --help
```

## Delivery Checks

Before handing off or packaging this backend, run the project doctor:

```bash
./doctor.sh
```

The doctor performs the current minimum delivery gate:

- Python syntax checks for the backend service and contract validator
- EvoFlow CLI smoke test
- local .NET SDK check
- `OperatorRunner` build with nullable warnings enabled
- demo asset presence checks
- backend render-contract unit tests
- sample Unity render-contract validation

You can also validate a single export directly:

```bash
./scripts/validate_render_contract.py ./exports/test3.json --limit 1000 --no-selected-ids
```

For local Unity/backend-service integration, start:

```bash
./run_backend_server.sh
```

Useful service endpoints:

- `GET http://127.0.0.1:8000/api/health`
- `GET http://127.0.0.1:8000/api/datasets`
- `GET http://127.0.0.1:8000/api/render/test3?limit=1000&includeSelectedIds=false`
- `GET http://127.0.0.1:8000/api/render/test3?summary=true`

## Example Result

On the Hurricane Sandy sample with a backend-ready point-hotspot task, the current system can produce:

- `ViewType: Point`
- `BackendBuilt: True`
- `EncodeTimeOperator + ApplySpatialFilterOperator + ApplyTemporalFilterOperator + CombineFiltersOperator + AdaptedIATKViewBuilderOperator`
- a final exported Unity-facing JSON with `schemaVersion: 2.0.0`

Recent example scores:

- `ExecutionScore: 0.6025`
- `LLMScore: 0.6`
- `Fitness: 0.756`

## Unity Export

The backend currently exports a Unity-facing JSON contract with this top-level structure:

```json
{
  "meta": {},
  "task": {},
  "selectedWorkflow": {},
  "visualization": {},
  "resultSummary": {}
}
```

The most important section is `visualization`, which now uses a more explicit fixed structure:

- `intent`
- `renderPlan`
- `dataSummary`
- `semanticSummary`

For lightweight schema alignment there is also a smaller sample file:

- [test3_schema_sample.json](exports/test3_schema_sample.json)

For real runtime-side integration, use the full artifact:

- [test3.json](exports/test3.json)

More detail:

- [UNITY_EXPORT_README_CN.md](Docs/UNITY_EXPORT_README_CN.md)

## Current Status

What is already working:

- natural-language task input
- CSV schema inference with heuristic + LLM-assisted fallback path
- workflow search over real C# operators
- C# runner execution
- LLM-assisted evaluation
- weakly supervised result scoring when no `expectedRowIds` are available
- Unity-facing export JSON generation
- local backend service for Unity consumption
- automated backend doctor and render-contract validation

What is still improving:

- hotspot quality and concentration
- LLM timeout / retry stability
- broader cross-dataset generalization
- Unity Editor/build rendering validation
- packaging layout for a standalone desktop app

## Suggested Repo Name

Recommended GitHub repository name:

- `evoflow-vis-runtime`

Other acceptable alternatives:

- `evoflow-vis-backend`
- `nl2vis-operator-runtime`
- `evoflow-unity-export`

## Project Notes

Supporting project notes are organized under `Docs/`:

- [UPDATE_EVOFLOW_OPERATOR_WORKFLOW_CN.md](Docs/UPDATE_EVOFLOW_OPERATOR_WORKFLOW_CN.md)
- [EVOFLOW_INTERFACE_SPEC_CN.md](Docs/EVOFLOW_INTERFACE_SPEC_CN.md)
- [ADVISOR_NOTE_CN.md](Docs/ADVISOR_NOTE_CN.md)

## Summary

This repository is currently best understood as a **backend research prototype** for:

- natural-language visualization planning
- operator workflow search
- C# visualization execution
- LLM-assisted evaluation
- Unity-facing result export

It is already beyond a toy demo, but it is still an actively evolving prototype rather than a finished product.
