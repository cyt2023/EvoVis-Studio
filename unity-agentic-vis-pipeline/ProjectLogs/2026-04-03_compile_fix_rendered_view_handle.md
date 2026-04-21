# 2026-04-03 Compile Fix RenderedViewHandle

## Summary

Fixed a compile error in `RenderedViewHandle` caused by a missing namespace import for `PointRenderModel`.

## Existing Files Modified

- `Assets/Scripts/Integration/Rendering/RenderedViewHandle.cs`

## What Changed

- added `using ImmersiveTaxiVis.Integration.Models;`

## Why

`RenderedViewHandle.MatchesGeometry(...)` uses `PointRenderModel`, so the file needs the integration models namespace in scope.

This fixes:

- `CS0246: The type or namespace name 'PointRenderModel' could not be found`
