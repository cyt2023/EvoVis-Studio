# 2026-04-03 Frontend Render Coordinator

## Summary

Added a thin render coordination layer so the Unity side moves one step closer to a backend-driven frontend architecture instead of keeping render orchestration inside the demo controller.

## Goal

Restructure the current minimal JSON path toward:

`result source -> parser -> mapper -> render coordinator -> renderer -> rendered view state`

## New Files Added

- `Assets/Scripts/Integration/Rendering/RenderedViewHandle.cs`
- `Assets/Scripts/Integration/Rendering/FrontendRenderCoordinator.cs`
- `ProjectLogs/2026-04-03_frontend_render_coordinator.md`

## Existing Files Modified

- `Assets/Scripts/Integration/Rendering/IatkJsonViewRenderer.cs`
- `Assets/Scripts/Integration/Controllers/JsonResultDemoController.cs`

## What Changed

- `IatkJsonViewRenderer` now returns a `RenderedViewHandle` instead of only returning a root `GameObject`
- the handle stores references to rendered point and link views
- `JsonResultDemoController` no longer directly performs load/map/render orchestration
- new `FrontendRenderCoordinator` now owns:
  - load
  - map
  - clear
  - render execution result reporting

## Why This Matters

This keeps the rendering pipeline closer to the frontend architecture the project now needs:

- Unity consumes backend results
- Unity maps those results into render models
- Unity renders through a dedicated coordinator
- future HTTP result loading can be added without changing rendering core
- future state updates can attach to rendered view handles

## Current Limitations

- still uses full clear-and-rerender
- no dedicated clean frontend scene yet
- no incremental point/link update coordinator yet

## Next Staged Work

- add a dedicated lightweight frontend demo scene
- add selection/highlight incremental update path using `RenderedViewHandle`
- split renderer by view type when more view types arrive
