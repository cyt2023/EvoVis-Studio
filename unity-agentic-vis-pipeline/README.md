# EvoVis Studio

## Overview

This repository is the Unity desktop frontend of *EvoVis Studio*, a new agentic visualization project built on top of the original *Immersive TaxiVis* research prototype.

The current work keeps the original project’s most important visualization-core ideas:
- OD trip data as the main data model
- Space-Time Cube style view construction
- coordinated point, link, and projection views
- query and filter driven visual updates
- Adapted IATK as the rendering foundation

On top of that base, this version adds a workflow execution path so that standardized EvoFlow-style JSON can be read inside Unity, resolved to local data, executed through a runtime operator chain, and then rendered through the existing frontend pipeline.

## Original Project Reference

This project is based on the original *Immersive TaxiVis* work by the paper authors.

Relevant references:
- Original research paper preprint: https://arxiv.org/abs/2402.00344
- Supplementary video: https://www.youtube.com/watch?v=5nQFVHqUBaU
- Original *TaxiVis* system: https://github.com/VIDA-NYU/TaxiVis

The current repository is an adaptation and extension of that original Unity prototype, rather than a new project from scratch.

## Current Scope

The current repository focuses on the visualization core and the new agentic workflow layer.

In particular, the current extension emphasizes:
- standardized workflow JSON input
- Unity-side data-path resolution
- runtime operator execution
- backend-result adaptation
- reuse of the existing Unity rendering path

The current focus is not on restoring the original immersive shell features such as VR interaction, maps, MRTK workflows, or Bing Maps integration.

## Current Agentic Workflow Path

The current embedded execution path is:

`workflow JSON -> data path resolution -> runtime operator execution -> backend-result adaptation -> existing Unity renderer`

The preferred desktop-app direction is now a local backend-service path:

`Unity desktop app -> local EvoFlow backend service -> Unity-ready render JSON -> existing Unity renderer`

In this path Unity should normally send `viewType: Auto`. EvoFlow infers the
primary view type from the task, and the backend service adapts the result into a
Unity-ready render contract before the frontend draws it. The current render
contract supports:

- flat point and hotspot views
- origin-destination link views
- Space-Time Cube views with normalized temporal height
- flat 2D projection views

Primary integrated test assets:
- `Assets/StreamingAssets/Agentic/Workflows/test3_workflow.json`
- `Assets/StreamingAssets/Agentic/Data/hurricane_sandy_2012_100k_sample.csv`
- `Assets/Scripts/Agentic/Unity/WorkflowRuntimeDemoController.cs`
- `Assets/Scripts/Agentic/Unity/BackendServiceRenderController.cs`

## Main Runtime Additions

The main additions for the agentic workflow path are:
- `Assets/Scripts/Agentic/Operators/*`
- `Assets/Scripts/Agentic/Unity/*`

These additions allow Unity to read an EvoFlow-style workflow description, execute the selected operator chain, and pass the result to the existing frontend rendering pipeline.


## Local Backend Service Mode

The desktop-app architecture can also run with EvoFlow as a local backend service. In this mode, Unity is the desktop visualization frontend, while the backend service returns workflow or render JSON over HTTP.

Backend project path:

`../OperatorsDraft`

Start the backend service from the parent workspace:

```bash
cd ../OperatorsDraft
./run_backend_server.sh
```

Available local endpoints:
- `GET http://127.0.0.1:8000/api/health`
- `GET http://127.0.0.1:8000/api/workflow/test3`
- `GET http://127.0.0.1:8000/api/render/test3`
- `POST http://127.0.0.1:8000/api/render/run`

Unity-side entry point:
- `Assets/Scripts/Agentic/Unity/BackendServiceRenderController.cs`
- `Assets/Scripts/Agentic/Unity/BackendCommandWindowController.cs`

This path avoids treating Unity as the main operator runtime. Unity requests a backend result from EvoFlow and then renders the returned JSON through the existing frontend pipeline.


## Desktop App Mode

The current project can be assembled as a local desktop app rather than a browser application. In this mode, Unity is the desktop frontend, and EvoFlow runs as a local backend process.

Recommended runtime chain:

`DesktopAgenticAppBootstrap -> DesktopBackendServiceController -> BackendCommandWindowController -> local EvoFlow backend -> Unity renderer`

Key desktop-app scripts:
- `Assets/Scripts/Agentic/Unity/DesktopAgenticAppBootstrap.cs`
- `Assets/Scripts/Agentic/Unity/DesktopBackendServiceController.cs`
- `Assets/Scripts/Agentic/Unity/BackendCommandWindowController.cs`
- `Assets/Scripts/Integration/Editor/FrontendDemoSceneCreator.cs`

What this adds:
- Unity can auto-start the local EvoFlow backend on desktop builds and in the Editor.
- The in-app command window can start, restart, and stop the owned backend process.
- The frontend can wait for backend health before requesting render JSON.
- Unity Editor now has menu entries to create/open a dedicated desktop agentic app scene.

Packaging note:
- In the current workspace layout, `unity-agentic-vis-pipeline` and `OperatorsDraft` are sibling folders.
- For a standalone build, keep an `OperatorsDraft` folder next to the built Unity app, or copy the backend into `StreamingAssets/EvoFlowBackend`.
- On Windows, `OperatorsDraft/run_backend_server.bat` is provided for local process launch.

## Workspace Docs

Workspace-level Chinese documentation is organized under `Docs/Workspace/`. Backend-side research notes are organized under `../OperatorsDraft/Docs/`.

## Folder Guide

- `Assets/AdaptedIATK/`
  Adapted IATK rendering foundation reused from the original project.

- `Assets/IATK-Plugins/`
  Third-party plugins and supporting packages required by the rendering stack.

- `Assets/MinorAssets/`
  External or supporting Unity assets used by the project.

- `Assets/Resources/`
  Unity resources loaded through the Resources system, including bundled data and materials.

- `Assets/StreamingAssets/`
  Runtime-readable files that Unity can access directly from disk.

- `Assets/StreamingAssets/Agentic/Workflows/`
  Standardized workflow JSON files used to drive the agentic execution path.

- `Assets/StreamingAssets/Agentic/Data/`
  Local test datasets referenced by workflow JSON files.

- `Assets/Scripts/Integration/`
  Existing Unity-side frontend integration, mapping, and rendering pipeline reused from the reduced TaxiVis frontend work.

- `Assets/Scripts/Agentic/Unity/`
  Unity-facing controllers and adapters for the new workflow execution path.

- `Assets/Scripts/Agentic/Operators/`
  Runtime operator implementation grouped by responsibility: `Core`, `Data`, `View`, `Query`, `Filter`, `Backend`, and `Runner`.

- `Assets/ProjectLogs/`
  Project log files mirrored inside Unity assets for in-project reference.

- `ProjectLogs/`
  Top-level implementation logs documenting development batches and integration progress.

## How To Test The Current Workflow Path

Recommended environment:
- Unity 2022.1.10f1 or a closely compatible version
- Windows for final IATK-based rendering validation

Current test flow:
1. Open the Unity project.
2. Open or create the desktop app scene.
3. Keep the command window's requested view type as `Auto`.
4. Run the scene.
5. Enter natural-language prompts for point, link, STC, or 2D projection views.
6. Verify the Unity Console log for the backend response primary view type.

Manual prompts used for current validation:

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

## Notes

This repository should be understood as an adaptation of the original *Immersive TaxiVis* codebase toward an agentic workflow execution model. The original project remains the conceptual basis; the current contribution is the workflow-oriented extension on top of that foundation.
