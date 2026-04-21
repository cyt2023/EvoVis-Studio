# 2026-04-20 Local Backend Service Integration / 本地后端服务集成

## English Summary

Added the first local backend-service path for the desktop-app architecture. The goal is to keep Unity as the desktop visualization frontend while exposing EvoFlow results through a lightweight local HTTP service.

## 中文摘要

新增了第一版本地后端服务路径，用于支持桌面 App 架构。目标是让 Unity 作为桌面可视化前端，而 EvoFlow 通过本地 HTTP 服务提供 workflow/render JSON。

## English Implementation Notes

- Added `OperatorsDraft/server.py` as a standard-library Python HTTP service.
- Added `OperatorsDraft/run_backend_server.sh` as a convenience launcher.
- Added `BackendWorkflowClient.cs` in Unity to request backend JSON through `UnityWebRequest`.
- Added `BackendServiceRenderController.cs` in Unity to fetch Unity-ready render JSON and pass it to the existing renderer.
- The backend currently exposes health, raw workflow, and Unity-ready render endpoints.

## 中文实现说明

- 在 `OperatorsDraft/server.py` 中新增了只依赖 Python 标准库的 HTTP 服务。
- 新增 `OperatorsDraft/run_backend_server.sh` 作为快捷启动脚本。
- 在 Unity 中新增 `BackendWorkflowClient.cs`，通过 `UnityWebRequest` 请求后端 JSON。
- 在 Unity 中新增 `BackendServiceRenderController.cs`，负责获取 Unity-ready render JSON 并交给现有渲染链。
- 当前后端提供健康检查、原始 workflow JSON、Unity 可渲染 JSON 三类接口。

## Endpoints / 接口

- `GET /api/health`
- `GET /api/workflow/test3`
- `GET /api/render/test3`

## Test Notes / 测试记录

- Python syntax validation passed with `py_compile` using a temporary cache prefix.
- The backend adapter converts `exports/test3.json` into a Unity-ready result with 69,404 points and 1,322 selected points.
- Unity Editor runtime validation is still required on the desktop build environment.

## Next Step / 下一步

Run the backend service, attach `BackendServiceRenderController` in a Unity scene, and verify that Unity can request `http://127.0.0.1:8000/api/render/test3` and render the returned point view.

## 2026-04-20 Follow-up / 后续补充

### English

Extended the backend service and Unity client with task-style POST support. Unity can now either request a fixed render result with `GET /api/render/test3`, or send a task request to `POST /api/render/run`. The current backend implementation uses `workflowId` to return an existing export, which keeps the external contract ready for later dynamic EvoFlow execution.

Validated `POST /api/render/run` locally: the endpoint returned Unity-ready JSON with 69,404 points, 1,322 selected points, and `Point` view type.

### 中文

扩展了后端服务和 Unity 客户端，加入任务式 POST 请求支持。Unity 现在既可以通过 `GET /api/render/test3` 获取固定渲染结果，也可以通过 `POST /api/render/run` 发送任务请求。当前后端会根据 `workflowId` 返回已有导出结果，这样后续可以在不改变 Unity 通信协议的情况下替换为真正的 EvoFlow 动态执行。

已在本地验证 `POST /api/render/run`：接口成功返回 Unity-ready JSON，包含 69,404 个点、1,322 个选中点，视图类型为 `Point`。

## 2026-04-20 Command Window Extension / 命令窗口扩展

### English

Added a Unity-side `BackendCommandWindowController` that displays a simple in-app command window. The user can enter a natural-language visualization task, choose dataset/workflow/view settings, and click `Run And Render` to send a POST request to the local EvoFlow backend.

The backend service now supports an optional `execute=true` mode. When disabled, the endpoint returns an existing workflow such as `test3`; when enabled, the backend calls `run_evoflow.sh` to generate a fresh export JSON before adapting it for Unity rendering.

### 中文

新增 Unity 侧 `BackendCommandWindowController`，提供一个简单的 App 内命令输入窗口。用户可以输入自然语言可视化任务，设置 dataset/workflow/view，然后点击 `Run And Render`，向本地 EvoFlow 后端发送 POST 请求。

后端服务现在支持可选的 `execute=true` 模式。关闭时，接口返回已有 workflow，例如 `test3`；开启时，后端会调用 `run_evoflow.sh` 生成新的导出 JSON，再转换成 Unity 可渲染结构。

## 2026-04-20 Dataset and Error Handling Optimization / 数据集与错误处理优化

### English

Added `GET /api/datasets` so Unity can discover available CSV datasets from the backend. The Unity command window can now load the dataset list and quickly select one of the available datasets instead of requiring manual filename entry.

Backend errors now use a standardized response shape with `status` and `error.stage/message/details`. Local verification confirmed that `/api/datasets` lists 3 CSV datasets, `/api/render/test3` still returns 69,404 points, and unknown routes return the standardized error format.

### 中文

新增 `GET /api/datasets`，让 Unity 可以从后端发现当前可用的 CSV 数据集。Unity 命令窗口现在可以加载数据集列表，并快速选择可用数据集，不再只能手动输入文件名。

后端错误现在统一返回 `status` 与 `error.stage/message/details` 结构。本地验证确认：`/api/datasets` 可以列出 3 个 CSV 数据集，`/api/render/test3` 仍然返回 69,404 个点，未知路径会返回标准错误格式。

## 2026-04-21 Unity-side Error Parsing Hardening / Unity 侧错误解析加固

### English

Improved the Unity frontend so it no longer assumes every successful HTTP response is immediately renderable. The backend client now detects the standardized `status: failed` error envelope, extracts `stage/message/details`, and surfaces that message in both the console and the in-app command window.

Added a shared render-result validation path to reject empty or malformed responses before they reach the renderer. This makes local debugging much easier when EvoFlow execution fails, a workflow id is missing, or the backend returns a non-renderable payload.

### 中文

加强了 Unity 前端对后端响应的判断逻辑，不再默认只要 HTTP 请求成功就一定可以直接渲染。现在后端客户端会识别标准化的 `status: failed` 错误包，提取 `stage/message/details`，并把更可读的错误信息展示到 Unity Console 和 App 内命令窗口。

同时新增统一的 render 结果校验逻辑，在进入渲染器前先拦截空响应、错误响应和缺少视图数据的响应。这样在 EvoFlow 执行失败、workflowId 不存在或后端返回结构不完整时，更容易定位问题。
