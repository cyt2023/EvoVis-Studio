# 2026-04-03 Compile Fix JsonResultDemoController

## Summary

Fixed a compile error introduced during the render coordinator refactor.

## Existing Files Modified

- `Assets/Scripts/Integration/Controllers/JsonResultDemoController.cs`

## What Changed

- replaced the stale `EnsureCoordinator()` call in the `catch` block
- updated it to the current coordinator setup path:
  - `EnsureRenderRoot()`
  - `RebuildCoordinator()`

## Why

The controller had already been refactored to use `RebuildCoordinator()`, but one old method reference remained and caused:

- `CS0103: The name 'EnsureCoordinator' does not exist in the current context`

## Notes

- this was a targeted compile fix only
- no rendering core files were changed
