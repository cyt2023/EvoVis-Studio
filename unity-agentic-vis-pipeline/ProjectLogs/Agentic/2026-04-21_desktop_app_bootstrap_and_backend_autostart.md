# 2026-04-21 Desktop App Bootstrap And Backend Autostart / 桌面 App 启动器与后端自动拉起

## English Summary

Added a desktop-app oriented runtime layer so Unity can manage the local EvoFlow backend process directly instead of requiring a manual terminal step before every run.

## 中文摘要

新增面向桌面 App 的运行时控制层，让 Unity 可以直接管理本地 EvoFlow 后端进程，而不再要求每次运行前都先手动打开终端启动服务。

## English Implementation Notes

- Added `DesktopBackendServiceController.cs` to start, restart, stop, and health-check a local backend process.
- Added `DesktopAgenticAppBootstrap.cs` so an empty scene can be turned into a runnable desktop-app entry scene with camera, light, backend controller, and command window.
- Updated `BackendCommandWindowController.cs` so the in-app command panel can manage the backend process and wait for it before sending tasks.
- Updated `BackendServiceRenderController.cs` so render-on-start mode can also auto-start the backend and wait for readiness.
- Added `OperatorsDraft/run_backend_server.bat` for Windows launch compatibility.

## 中文实现说明

- 新增 `DesktopBackendServiceController.cs`，用于启动、重启、停止并健康检查本地后端进程。
- 新增 `DesktopAgenticAppBootstrap.cs`，让一个空场景也能快速变成可运行的桌面 App 入口场景，自动补齐相机、光照、后端控制器和命令窗口。
- 更新 `BackendCommandWindowController.cs`，使 App 内命令面板可以直接管理后端进程，并在发任务前等待后端就绪。
- 更新 `BackendServiceRenderController.cs`，使自动渲染模式同样支持先自动拉起后端、再等待健康检查通过。
- 新增 `OperatorsDraft/run_backend_server.bat`，便于 Windows 本地启动。

## Current Result / 当前结果

The project is now closer to a standalone desktop-app workflow:

`Launch Unity app -> ensure/start local EvoFlow backend -> enter task -> request render JSON -> render inside Unity`

项目现在更接近独立桌面 App 的工作流：

`启动 Unity App -> 确保/拉起本地 EvoFlow 后端 -> 输入任务 -> 请求 render JSON -> 在 Unity 内渲染`

## Remaining Gap / 仍需完成

The remaining work is Unity Editor/build validation on the target desktop environment, especially for final scene composition, camera framing, and packaging layout.

剩余工作主要是目标桌面环境中的 Unity Editor / build 实测，尤其是最终场景构图、相机取景以及打包后的目录布局验证。

## Follow-up Improvement / 后续增强

### English

Added an Editor-side scene creator for the desktop app path, so the Unity project can generate or open a dedicated `DesktopAgenticApp` scene from the Tools menu instead of relying on manual scene setup every time.

Improved the in-app command window with backend diagnostics, including endpoint display, resolved backend root display, layout hints, and quick presets for cached and dynamic point-view runs.

### 中文

补充了桌面 App 路径的 Editor 场景创建工具，现在可以直接通过 Tools 菜单生成或打开专用的 `DesktopAgenticApp` 场景，不再需要每次手工搭场景。

同时增强了 App 内命令窗口的后端诊断信息，包括 endpoint 显示、backend 根目录显示、目录布局提示，以及 cached / dynamic 两种快速 preset。
