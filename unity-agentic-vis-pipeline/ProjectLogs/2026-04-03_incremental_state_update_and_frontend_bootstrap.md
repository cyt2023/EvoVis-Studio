# 2026-04-03 Incremental State Update And Frontend Bootstrap

## Summary

Added a first incremental state update path and a lightweight frontend scene bootstrap so the reduced Unity project can behave more like a backend-driven frontend host.

## New Files Added

- `Assets/Scripts/Integration/Controllers/FrontendDemoSceneBootstrap.cs`
- `ProjectLogs/2026-04-03_incremental_state_update_and_frontend_bootstrap.md`

## Existing Files Modified

- `Assets/Scripts/Integration/Rendering/RenderedViewHandle.cs`
- `Assets/Scripts/Integration/Rendering/IatkJsonViewRenderer.cs`
- `Assets/Scripts/Integration/Rendering/FrontendRenderCoordinator.cs`
- `Assets/Scripts/Integration/Controllers/JsonResultDemoController.cs`

## What Changed

- `RenderedViewHandle` now stores geometry identity information:
  - view type
  - point count
  - link count
  - point positions
- coordinator now attempts a state-only update before clearing and rebuilding
- state-only update currently applies when:
  - same number of views
  - same view names
  - same point count
  - same link count
  - same point positions
- when compatible, the coordinator updates:
  - point colors
  - point size channel
  - visibility
- `JsonResultDemoController` now preserves the coordinator when render configuration has not changed
- added `FrontendDemoSceneBootstrap` as a lightweight empty-scene host helper for future clean frontend demo scenes

## Why This Matters

This is the first step away from always doing full clear-and-redraw. It prepares the Unity side for a more frontend-like rendering lifecycle:

- consume backend result
- detect whether geometry changed
- update state when possible
- rebuild only when necessary

## Current Limitations

- links are still rebuilt only through full rerender
- geometry changes still trigger full rerender
- no dedicated `.unity` frontend scene asset has been added yet

## Suggested Usage

- create an empty scene
- add one GameObject with `FrontendDemoSceneBootstrap`
- let it create a camera, light, and `JsonResultDemoController`
- use that as the future clean backend frontend demo scene
