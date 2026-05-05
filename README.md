# EvoVis Studio

EvoVis Studio is a desktop-oriented agentic visualization project that connects:

- natural-language task input
- workflow search and execution on the backend
- JSON-based result exchange
- Unity-based interactive visualization rendering on the frontend

This repository is an independent project-level codebase. It includes both sides of the system in one top-level project:

- a Unity desktop visualization frontend
- an EvoFlow-style backend runtime and local service

The two folders are regular directories in this repository, not submodules and not separate repositories from the point of view of this project.

The current direction is not a browser application. The intended form is a desktop app workflow in which Unity acts as the visualization frontend and the backend provides workflow and render results locally.

## Project Origin

EvoVis Studio is based on two earlier project parts that were developed separately:

- Backend foundation: [`cyt2023/evoflow-vis-runtime`](https://github.com/cyt2023/evoflow-vis-runtime)
- Unity frontend foundation: [`cyt2023/unity-agentic-vis-pipeline`](https://github.com/cyt2023/unity-agentic-vis-pipeline)

This repository brings those parts together as one deliverable project. The source is copied into `OperatorsDraft/` and `unity-agentic-vis-pipeline/` so the project can be cloned, reviewed, tested, and handed off as a single repository.

## Project Goal

The goal of EvoVis Studio is to support a pipeline like this:

`user command -> backend workflow generation/execution -> workflow/render JSON -> Unity rendering`

In practice, this means a user can describe a visualization task in natural language, the backend can determine or execute a suitable workflow, and Unity can then render the result through an existing visualization pipeline.

This repository is especially focused on:

- agentic workflow execution
- OD-oriented visualization logic
- Space-Time Cube and related coordinated views
- desktop-app style frontend/backend integration

## Repository Layout

This repository contains two main subprojects.

### `unity-agentic-vis-pipeline/`

This is the Unity desktop frontend project.

It contains:

- the Unity project itself (`Assets/`, `Packages/`, `ProjectSettings/`)
- the adapted visualization frontend
- Unity-side integration code for JSON-driven rendering
- local backend-service client/controller scripts
- project logs and frontend-facing documentation

Use this subproject when you want to:

- open the Unity project in the Editor
- test the desktop visualization frontend
- render backend-generated workflow/render JSON
- work on runtime controllers, scene bootstrapping, and frontend rendering behavior

Important starting points inside this project include:

- `Assets/Scripts/Agentic/Unity/`
- `Assets/Scripts/Integration/`
- `Docs/Workspace/`
- `ProjectLogs/`

### `OperatorsDraft/`

This is the backend-side project.

It contains:

- EvoFlow-style workflow search logic
- backend operator definitions and runtime execution code
- the local HTTP backend service
- demo datasets
- exported JSON examples
- backend-side documentation and notes

Use this subproject when you want to:

- generate or test workflow results from natural-language tasks
- inspect operator search and execution logic
- run the local backend service
- inspect export JSON files used by Unity

Important starting points inside this project include:

- `evoflow/`
- `operators/`
- `OperatorRunner/`
- `server.py`
- `run_evoflow.sh`
- `run_backend_server.sh`
- `Docs/`

## System Architecture

The current preferred architecture is:

`Unity desktop app -> local EvoFlow backend service -> workflow/render JSON -> Unity renderer`

More explicitly:

1. A user enters a task or command.
2. The backend selects or executes a workflow.
3. The backend returns a structured JSON result.
4. Unity reads that result.
5. Unity maps the result into renderable views.
6. Unity renders point, link, STC, or projection-based visual views.

The local backend service now returns Unity-ready render geometry for the main
view families. Unity requests should normally keep `viewType` set to `Auto`,
allowing EvoFlow to infer the correct `Point`, `Link`, `STC`, or `Projection2D`
view from the natural-language task. The backend preserves that inferred view
type and normalizes the geometry contract before Unity renders it:

- `Point` and hotspot-style requests render as flat point layers.
- `Link` requests render origin-destination line geometry.
- `STC` requests render time-normalized height in the returned `z` coordinate.
- `Projection2D` requests render flat projected point geometry.

## Packaging The Desktop App

For a Windows desktop build, package the Unity frontend together with the local EvoFlow backend:

1. Prepare the bundled backend:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\prepare_unity_backend_bundle.ps1
```

This copies `OperatorsDraft/` into `unity-agentic-vis-pipeline/Assets/StreamingAssets/EvoFlowBackend` while excluding local secrets, caches, and generated build folders.

2. Open `unity-agentic-vis-pipeline/` in Unity.

3. Use `File > Build Settings`.

4. Confirm the enabled scene is:

```text
Assets/Scenes/Agentic/DesktopAgenticApp.unity
```

5. Select `PC, Mac & Linux Standalone`, target Windows, then build.

6. On the target machine, configure the DashScope key before launching the app:

```powershell
[Environment]::SetEnvironmentVariable("DASHSCOPE_API_KEY", "your_sk_api_key", [EnvironmentVariableTarget]::User)
[Environment]::SetEnvironmentVariable("DASHSCOPE_MODEL", "qwen-turbo", [EnvironmentVariableTarget]::User)
```

Restart the app after setting environment variables. The real API key should stay in local environment variables or an ignored local `.env.local`, not in source control or the Unity build repository.

## Frontend / Backend Responsibilities

### Backend responsibilities

The backend side is responsible for:

- understanding or receiving the task
- choosing or executing the operator workflow
- reading datasets
- producing workflow results
- exporting render-oriented JSON
- serving results through a local HTTP interface when running in desktop mode

### Unity responsibilities

The Unity side is responsible for:

- requesting backend data or render JSON
- validating and parsing returned results
- mapping backend data into Unity render models
- dispatching to the appropriate renderer
- presenting the final visualization in a desktop application environment

## Current Runtime Direction

At the moment, there are two closely related ways to use the system.

### 1. Export-first path

In this path, the backend generates or exports JSON first, and Unity consumes that artifact later.

This is useful for:

- debugging export structure
- validating schema and render payloads
- testing Unity rendering with stable sample outputs

### 2. Local backend-service path

In this path, Unity behaves more like a desktop application frontend and requests results from a local backend service on demand.

This is useful for:

- command-driven visualization workflows
- desktop-app style interaction
- tighter frontend/backend integration

The current repository is increasingly optimized for this second path.

## Quick Start

### Option A: Start from the Unity frontend

1. Open `unity-agentic-vis-pipeline/` in Unity.
2. Use the desktop app bootstrap/runtime scripts.
3. Start or connect to the local backend service.
4. Request and render a workflow result.

See:

- `unity-agentic-vis-pipeline/README.md`
- `unity-agentic-vis-pipeline/Docs/Workspace/DESKTOP_APP_RUNTIME_CN.md`
- `unity-agentic-vis-pipeline/Docs/Workspace/TESTING_STAGES_CN.md`

### Option B: Start from the backend

1. Enter `OperatorsDraft/`.
2. Run the backend search/export pipeline or local service.
3. Inspect the generated JSON or serve it to Unity.

Useful files:

- `OperatorsDraft/run_evoflow.sh`
- `OperatorsDraft/run_backend_server.sh`
- `OperatorsDraft/server.py`
- `OperatorsDraft/README.md`

## Desktop App Workflow

The intended desktop usage pattern is:

1. Unity launches as the desktop frontend.
2. Unity ensures the local backend is available.
3. The user enters a task.
4. The backend returns a workflow/render result.
5. Unity renders the result.

This is the practical meaning of “desktop app” in the current repository.

Rather than moving everything into the browser, the system keeps rendering and interaction in Unity while treating the workflow side as a local service/runtime layer.

## Current Capabilities

The repository currently includes work toward:

- JSON-driven visualization execution
- local backend-service communication
- desktop runtime bootstrap and backend autostart
- Unity-side render dispatch for supported views
- strict Unity-ready render JSON mapping for point, link, STC, and 2D projection views
- backend-side workflow search and export logic
- OD-oriented and STC-related visualization concepts inherited from the adapted TaxiVis foundation

Useful command-window prompts for manual testing:

```text
Show taxi dropoff hotspots as a point visualization. Use dropoff longitude and latitude and highlight concentrated destination areas.
```

```text
Render taxi trips as origin-destination links. Draw lines from pickup locations to dropoff locations and show movement patterns across the city.
```

```text
Show all taxi trips in a space-time cube. Use pickup longitude and latitude on the ground plane and pickup time on the vertical axis. Do not filter rows.
```

```text
Show all taxi pickup points as a 3D point visualization. Use pickup longitude and latitude on the ground plane, and use fare amount or trip distance as height. Do not filter rows.
```

## Included Documentation

### Top-level project docs

- `unity-agentic-vis-pipeline/Docs/Workspace/DESKTOP_APP_RUNTIME_CN.md`
- `unity-agentic-vis-pipeline/Docs/Workspace/TESTING_STAGES_CN.md`

### Backend-side docs

- `OperatorsDraft/Docs/ADVISOR_NOTE_CN.md`
- `OperatorsDraft/Docs/EVOFLOW_INTERFACE_SPEC_CN.md`
- `OperatorsDraft/Docs/UNITY_EXPORT_README_CN.md`
- `OperatorsDraft/Docs/UPDATE_EVOFLOW_OPERATOR_WORKFLOW_CN.md`

### Project logs

- `unity-agentic-vis-pipeline/ProjectLogs/`
- `unity-agentic-vis-pipeline/Assets/ProjectLogs/`

## Current Status

This repository should currently be understood as:

- beyond a toy prototype
- not yet a polished production product
- actively shaped around a desktop-app research and development workflow

The main value of this repository is that it already combines:

- the frontend Unity project
- the backend EvoFlow/runtime project
- the JSON contract layer between them

into a single top-level project structure.

## Notes

- This top-level repository is intentionally a combined project snapshot.
- It does not replace the conceptual identity of the original source projects, but it does provide a practical “whole system” repository for EvoVis Studio.
- Some export artifacts are large and may later need cleanup or Git LFS if the repository is further polished for public distribution.

## Subproject READMEs

For more detail, continue with:

- `unity-agentic-vis-pipeline/README.md`
- `OperatorsDraft/README.md`
