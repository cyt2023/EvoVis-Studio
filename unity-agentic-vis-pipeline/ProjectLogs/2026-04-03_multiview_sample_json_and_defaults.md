# 2026-04-03 Multiview Sample Json And Defaults

## Summary

Added a multi-view sample backend result and updated the new frontend entry points to default to that sample.

## Existing Files Modified

- `Assets/Scripts/Integration/Controllers/JsonResultDemoController.cs`
- `Assets/Scripts/Integration/Controllers/FrontendDemoSceneBootstrap.cs`
- `Assets/Scripts/Integration/Editor/FrontendDemoSceneCreator.cs`

## New Files Added

- `Assets/StreamingAssets/result_multiview.json`
- `ProjectLogs/2026-04-03_multiview_sample_json_and_defaults.md`

## What Changed

- added a multi-view backend result sample containing:
  - `STC`
  - `POINT`
  - `PROJECTION2D_XY`
  - `PROJECTION2D_XZ`
  - `LINK`
- changed new frontend defaults to use `result_multiview.json`

## Why

The integration layer now supports renderer dispatch by backend view type, so the default test flow should exercise multiple view types instead of only one STC-like view.
