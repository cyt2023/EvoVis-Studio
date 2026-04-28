# 2026-04-24 Qwen Unity Standalone Packaging

## Summary

This update turns the Unity + EvoFlow prototype into a reproducible desktop application workflow. The backend now runs through DashScope compatible-mode `qwen-turbo`, Unity launches the local backend automatically, and Windows packaging includes bundled backend executables instead of requiring manual Python startup.

## Changes

- Switched dynamic LLM execution from the earlier app-style integration to DashScope compatible-mode chat completions with `qwen-turbo`.
- Standardized environment-variable loading around `DASHSCOPE_API_KEY` and optional `DASHSCOPE_MODEL`, with `.env.example` and ignored local `.env.local` support.
- Updated EvoFlow task execution to support packaged/frozen mode:
  - packaged `EvoFlowRunner.exe`
  - packaged `EvoFlowBackend.exe`
  - self-contained `OperatorRunnerPublished/OperatorRunner.exe`
- Added stricter `--require-llm` handling so dynamic workflow execution can fail fast instead of silently falling back when a real LLM is required.
- Added Unity desktop app defaults for direct natural-language execution against the local backend.
- Updated Unity backend requests to pass point limits and LLM-required flags consistently.
- Improved Unity-side backend result mapping so numeric attributes can drive point height, color, and visible 3D structure.
- Reworked runtime rendering to show a clearer XYZ frame and more legible point cloud defaults.
- Added standalone-safe rendering fallbacks so packaged builds do not crash when IATK resources are missing.
- Explicitly added key IATK shaders to Unity `GraphicsSettings.asset` so standalone builds stay closer to editor rendering.
- Added Windows packaging scripts:
  - `scripts/prepare_unity_backend_bundle.ps1`
  - `scripts/build_windows_backend_bundle.ps1`
- Updated README instructions for:
  - local DashScope configuration
  - Unity desktop packaging
  - target-machine environment setup

## Verification

Verified during this delivery cycle:

- qwen-turbo returned successful live responses for EvoFlow task parsing and workflow evaluation.
- `/api/render/run` produced Unity render payloads with point data for taxi datasets.
- Unity Editor successfully rendered command-driven 3D point views.
- Windows backend bundle build completed successfully with:
  - `EvoFlowBackend.exe`
  - `EvoFlowRunner.exe`
  - `OperatorRunnerPublished/`
- Unity standalone packaging path was validated up to the generated desktop executable.

## Remaining Risks

- Standalone rendering still depends on the target Windows machine supporting the bundled Unity/IATK rendering stack consistently.
- DashScope availability, latency, and quota still affect dynamic natural-language workflow generation.
- The shipped desktop app still requires a valid local `DASHSCOPE_API_KEY` on the target machine.
