# 2026-04-03 JSON Local Integration

## Summary

Added a minimal integration layer so the reduced Unity project can load a backend-style JSON result from local disk and render a first working visualization path without restoring the old VR / Maps application structure.

## Goal

Implemented the first path:

`StreamingAssets/result.json -> parse -> map -> render`

## Existing Reuse Points

- `Assets/AdaptedIATK/Scripts/Controller/ViewBuilder.cs`
- `Assets/AdaptedIATK/Scripts/View/View.cs`
- `Assets/AdaptedIATK/Scripts/View/BigMesh.cs`
- `Assets/AdaptedIATK/Scripts/Util/IATKUtil.cs`

These were reused as the thinnest available rendering entry points in the reduced project.

## New Files Added

- `Assets/Scripts/Integration/Models/BackendResultModels.cs`
- `Assets/Scripts/Integration/Models/MappedViewModels.cs`
- `Assets/Scripts/Integration/IO/LocalJsonResultLoader.cs`
- `Assets/Scripts/Integration/Mapping/BackendResultMapper.cs`
- `Assets/Scripts/Integration/Rendering/IatkJsonViewRenderer.cs`
- `Assets/Scripts/Integration/Controllers/JsonResultDemoController.cs`
- `Assets/StreamingAssets/result.json`

## Existing Files Modified

None.

## Current Supported Path

- Local JSON loading from `StreamingAssets/result.json`
- Unity-side parsing via `JsonUtility`
- Mapping into a lightweight point-based render model
- Rendering through existing AdaptedIATK mesh/view pipeline
- Selection/highlighting from JSON through point color and size
- Basic direct line rendering for `links` when provided

## Current Limitations

- No HTTP / WebSocket / backend networking yet
- No restoration of old VR / Bing Maps / MRTK application flow
- No projection views yet
- No incremental update flow yet
- Link rendering is lightweight and not the original full OD interaction workflow

## Manual Test Notes

- New blue / yellow points are expected to be the JSON-driven render output
- Existing white points in scene likely belong to the pre-existing visualization content
- Selected points render in yellow
- Non-selected points render in blue

## Next Staged Work

- Add optional render offset / scale controls to improve visibility in large scenes
- Add optional 2D projection rendering
- Add incremental refresh path for selection/highlight updates
- Expand schema support for richer encoding state
