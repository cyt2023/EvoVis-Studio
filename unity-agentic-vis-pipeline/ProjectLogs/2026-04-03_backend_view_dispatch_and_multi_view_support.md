# 2026-04-03 Backend View Dispatch And Multi View Support

## Summary

Expanded the Unity integration layer from a single-path point/STC renderer into a broader backend-result frontend with renderer dispatch and multiple backend view-type support.

## New Files Added

- `Assets/Scripts/Integration/Rendering/BackendViewRenderContext.cs`
- `Assets/Scripts/Integration/Rendering/IBackendViewRenderer.cs`
- `Assets/Scripts/Integration/Rendering/BackendViewRendererRegistry.cs`
- `Assets/Scripts/Integration/Rendering/PointBackendViewRenderer.cs`
- `Assets/Scripts/Integration/Rendering/StcBackendViewRenderer.cs`
- `Assets/Scripts/Integration/Rendering/Projection2DBackendViewRenderer.cs`
- `Assets/Scripts/Integration/Rendering/LinkBackendViewRenderer.cs`
- `ProjectLogs/2026-04-03_backend_view_dispatch_and_multi_view_support.md`

## Existing Files Modified

- `Assets/Scripts/Integration/Models/BackendResultModels.cs`
- `Assets/Scripts/Integration/Models/MappedViewModels.cs`
- `Assets/Scripts/Integration/Mapping/BackendResultMapper.cs`
- `Assets/Scripts/Integration/Rendering/RenderedViewHandle.cs`
- `Assets/Scripts/Integration/Rendering/IatkJsonViewRenderer.cs`
- `Assets/Scripts/Integration/Rendering/FrontendRenderCoordinator.cs`
- `Assets/Scripts/Integration/Controllers/FrontendDemoSceneBootstrap.cs`

## What Changed

- added backend view renderer dispatch architecture
- backend view types are now normalized and routed by dedicated renderers
- added first-class support for:
  - `STC`
  - `POINT`
  - `LINK`
  - `PROJECTION2D`
  - `PROJECTION2D_XY`
  - `PROJECTION2D_XZ`
  - `PROJECTION2D_YZ`
- added projection plane support in the backend contract and internal render model
- state-only update path now goes through the corresponding renderer instead of assuming every view is the same type

## Why

This moves the Unity project closer to the actual long-term frontend role:

- consume backend result JSON
- dispatch by view type
- render multiple frontend view forms
- keep backend workflow internals hidden from Unity

## Notes

- this is still an incremental frontend architecture, not a restoration of the original research prototype
- some advanced view semantics may still map to simplified render behavior for now
