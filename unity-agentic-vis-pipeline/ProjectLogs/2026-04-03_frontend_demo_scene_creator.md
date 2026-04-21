# 2026-04-03 Frontend Demo Scene Creator

## Summary

Added a lightweight editor utility to create and open a dedicated backend frontend demo scene, plus render root transform controls for easier scene-scale setup.

## New Files Added

- `Assets/Scripts/Integration/Editor/FrontendDemoSceneCreator.cs`
- `ProjectLogs/2026-04-03_frontend_demo_scene_creator.md`

## Existing Files Modified

- `Assets/Scripts/Integration/Controllers/JsonResultDemoController.cs`
- `Assets/Scripts/Integration/Controllers/FrontendDemoSceneBootstrap.cs`

## What Changed

- `JsonResultDemoController` now supports:
  - `renderRootLocalPosition`
  - `renderRootLocalScale`
- `FrontendDemoSceneBootstrap` now configures those render root transform values on the controller
- added editor menu items:
  - `Tools/ImmersiveTaxiVis/Create Backend Frontend Demo Scene`
  - `Tools/ImmersiveTaxiVis/Open Backend Frontend Demo Scene`
- the created scene is saved to:
  - `Assets/Scenes/Frontend/BackendFrontendDemo.unity`

## Why

This makes it easier to move testing away from legacy scenes and into a dedicated backend-driven visualization frontend scene without manually rebuilding the setup each time.
