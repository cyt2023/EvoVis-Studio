# 2026-05-05 Render Contract And View Dispatch Fixes

## Summary

This update fixes the natural-language-to-render path for multiple view types in the desktop EvoVis workflow.

The main issue was not that EvoFlow lacked point, link, or STC operators. EvoFlow was producing the correct primary view types in the backend result, but Unity-side defaults and renderer heuristics could override or reinterpret the JSON result. In practice this made different natural-language requests collapse into point-like output, made 2D projections appear as vertical point clouds, and made OD/STC links render incorrectly.

## What Changed

- Unity command requests now default to `Auto` view type instead of forcing `Point`.
- In the Unity Editor, `DesktopBackendServiceController` now prefers the sibling `OperatorsDraft` source backend before the bundled `StreamingAssets/EvoFlowBackend` copy.
- The backend service now treats `Auto`, `Default`, and `Inferred` as non-overrides and preserves EvoFlow's inferred `Point`, `Link`, `STC`, or `Projection2D` view type.
- The backend service now emits Unity-ready geometry:
  - `Point` and `Projection2D` use flat `z = 0` geometry.
  - `STC` uses normalized temporal height in `z`.
  - `Link` uses shared normalized XY coordinates for both origin and destination endpoints.
- Link indices are now adapted correctly from `originPointId` / `destinationPointId` as well as `originIndex` / `destinationIndex`.
- STC workflow construction now forces `EncodeTimeOperator` whenever the required view type is `STC` and a time column is available.
- Unity's `BackendResultMapper` no longer guesses display height from `time` or `colorValue`; it maps the JSON `x/y/z` geometry directly into Unity coordinates.
- `Projection2DBackendViewRenderer` now keeps projection output flat.
- Link rendering now has a Unity `LineRenderer` fallback so OD/STC links do not depend entirely on the IATK line shader path.
- Command-window debug logs now print the requested view type and backend response primary view type.

## Validation

Backend contract tests were expanded to cover:

- preserving EvoFlow view type when Unity requests `Auto`
- explicit view-type override behavior
- flat `Projection2D` geometry
- normalized `STC` height
- link index adaptation
- normalization of mixed raw/normalized XY coordinates before Unity rendering

Validation command:

```powershell
cd OperatorsDraft
python -m unittest tests.test_server_contract
```

Latest result:

```text
Ran 10 tests
OK
```

## Manual Test Commands

Useful Unity command-window prompts:

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

After changing backend service code, stop Play Mode and restart the local backend process. Otherwise Unity may still be connected to an old `python` or `EvoFlowBackend` process.
