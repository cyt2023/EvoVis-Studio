# 2026-04-09 Workflow Runtime JSON Execution

## Summary

Added a workflow-driven execution path so the Unity project can read an EvoFlow JSON export, resolve its referenced dataset, execute the selected operator chain inside Unity, and hand the result to the existing frontend rendering pipeline.

## Goal

Implemented the new path:

`StreamingAssets/Agentic/Workflows/test3_workflow.json -> workflow runtime -> CSV read -> operator execution -> backend-result adaptation -> existing renderer`

## Existing Reuse Points

- `Assets/Scripts/Integration/Models/BackendResultModels.cs`
- `Assets/Scripts/Integration/Mapping/BackendResultMapper.cs`
- `Assets/Scripts/Integration/Rendering/FrontendRenderCoordinator.cs`
- `Assets/AdaptedIATK/Scripts/Controller/ViewBuilder.cs`
- `Assets/AdaptedIATK/Scripts/View/View.cs`

These remain the render-side reuse points. The new workflow layer feeds them instead of replacing them.

## New Files Added

- `Assets/Scripts/Agentic/Operators/Core/*`
- `Assets/Scripts/Agentic/Operators/Data/*`
- `Assets/Scripts/Agentic/Operators/View/*`
- `Assets/Scripts/Agentic/Operators/Query/*`
- `Assets/Scripts/Agentic/Operators/Filter/*`
- `Assets/Scripts/Agentic/Operators/Backend/*`
- `Assets/Scripts/Agentic/Operators/Runner/*`
- `Assets/Scripts/Agentic/Unity/WorkflowRuntimeBackendResultAdapter.cs`
- `Assets/Scripts/Agentic/Unity/WorkflowRuntimeRenderCoordinator.cs`
- `Assets/Scripts/Agentic/Unity/WorkflowRuntimeDemoController.cs`
- `Assets/StreamingAssets/Agentic/Workflows/test3_workflow.json`
- `Assets/StreamingAssets/Agentic/Data/hurricane_sandy_2012_100k_sample.csv`

## Existing Files Modified

None. All Unity-project changes in this batch are additive.

## Current Supported Path

- Read EvoFlow JSON with `selectedWorkflow`, `parsedSpec`, and `meta.sourceDataPath`
- Resolve dataset path from the JSON value or from a workflow-local fallback path
- Execute the point-focused workflow defined in `test3_workflow.json`
- Adapt runtime output into the existing backend-result schema
- Render through the existing Unity frontend / AdaptedIATK integration path

## Current Limitations

- Final validation still requires opening the project in Unity Editor on Windows and running the scene
- The current tested target is the point-view workflow; STC and richer coordinated view cases still need live verification
- The workflow runtime is newly added to this project and has not yet been exercised through a Unity build pipeline

## Manual Test Notes

- `WorkflowRuntimeDemoController` now defaults to `Agentic/Workflows/test3_workflow.json`
- The workflow JSON stored under `StreamingAssets` uses a Unity-local relative CSV path
- On successful execution, the controller logs the resolved runtime data path and render summary

## Next Staged Work

- Verify Unity Editor compile/import behavior on Windows
- Run `test3_workflow.json` end-to-end in-scene and confirm point rendering
- Extend the same execution path to STC and coordinated-view workflows after the point path is confirmed
