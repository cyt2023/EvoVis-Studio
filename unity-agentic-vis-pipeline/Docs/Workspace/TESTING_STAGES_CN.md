# 分阶段测试说明

本文档记录当前 `untiy-project-hkust` 工作区中，EvoFlow 本地后端与 Unity 桌面前端的分阶段测试方法。

当前工作区结构：

```text
untiy-project-hkust/
  OperatorsDraft/                  EvoFlow / operators / 本地后端服务
  unity-agentic-vis-pipeline/      Unity 桌面前端项目
```

当前推荐架构：

```text
Unity 桌面 App -> 本地 EvoFlow 后端服务 -> workflow/render JSON -> Unity 前端渲染
```

---

## 阶段 0：确认目录和文件

### 目标

确认当前确实在正确项目中测试。

### 检查路径

后端项目：

```text
/Users/cyt/Desktop/untiy-project-hkust/OperatorsDraft
```

Unity 项目：

```text
/Users/cyt/Desktop/untiy-project-hkust/unity-agentic-vis-pipeline
```

### 应该看到的关键文件

后端侧：

```text
OperatorsDraft/server.py
OperatorsDraft/run_backend_server.sh
OperatorsDraft/exports/test3.json
```

Unity 侧：

```text
unity-agentic-vis-pipeline/Assets/Scripts/Agentic/Unity/BackendWorkflowClient.cs
unity-agentic-vis-pipeline/Assets/Scripts/Agentic/Unity/BackendServiceRenderController.cs
```

### 通过标准

这些文件都存在，即可进入下一阶段。

---

## 阶段 1：测试后端服务能否启动

### 目标

确认 EvoFlow 本地后端可以正常启动。

### 操作

在终端运行：

```bash
cd /Users/cyt/Desktop/untiy-project-hkust/OperatorsDraft
./run_backend_server.sh
```

### 预期输出

终端应显示类似：

```text
EvoFlow local backend running at http://127.0.0.1:8000
Available endpoints:
  GET /api/health
  GET /api/workflow/test3
  GET /api/render/test3
```

### 通过标准

终端停留在运行状态，没有立刻退出或报错。

### 常见问题

如果提示端口被占用，可以先关掉旧服务，或临时改用其他端口。

---

## 阶段 2：测试健康检查接口

### 目标

确认 Unity 后续可以访问本地后端。

### 操作

保持阶段 1 的后端服务运行，另开一个终端运行：

```bash
curl http://127.0.0.1:8000/api/health
```

### 预期输出

```json
{"status": "ok", "service": "evoflow-local-backend"}
```

### 通过标准

看到 `status: ok` 即通过。

---

## 阶段 2.5：测试数据集列表接口

### 目标

确认后端可以告诉 Unity 当前有哪些可用数据集，避免用户在 Unity 里手动输入文件名。

### 操作

```bash
curl http://127.0.0.1:8000/api/datasets
```

### 预期结果

返回结构类似：

```json
{
  "status": "success",
  "datasets": [
    {
      "id": "hurricane_sandy_2012_100k_sample.csv",
      "label": "hurricane sandy 2012 100k sample",
      "relativePath": "demo_data/hurricane_sandy_2012_100k_sample.csv",
      "rowCount": 100000
    }
  ]
}
```

### 通过标准

`datasets` 数组不为空。当前本地验证可发现 3 个 CSV 数据集。

---

## 阶段 3：测试原始 EvoFlow workflow JSON 接口

### 目标

确认后端可以返回原始 EvoFlow 输出。

### 操作

```bash
curl http://127.0.0.1:8000/api/workflow/test3
```

### 预期结果

返回内容应包含这些字段：

```json
{
  "meta": {...},
  "task": {...},
  "selectedWorkflow": {...},
  "visualization": {...},
  "resultSummary": {...}
}
```

### 通过标准

返回的是完整 JSON，且包含 `selectedWorkflow`、`visualization`、`resultSummary`。

---

## 阶段 4：测试 Unity-ready render JSON 接口

### 目标

确认后端可以把 EvoFlow 输出转换成 Unity 现有前端可渲染的 JSON。

### 操作

```bash
curl http://127.0.0.1:8000/api/render/test3
```

### 预期结果

返回内容应包含：

```json
{
  "meta": {...},
  "task": {...},
  "selectedWorkflow": {...},
  "visualizationPayload": {
    "views": [...]
  },
  "resultSummary": {...}
}
```

### 快速检查点

当前 `test3` 的转换结果应包含：

```text
viewType = Point
point count = 69404
selectedPointCount = 1322
```

### 通过标准

接口返回 `visualizationPayload.views`，并且里面有 `points` 数组。

---

## 阶段 4.5：测试任务式 POST 接口

### 目标

确认 Unity 后续不只是读取固定 `test3`，也可以把任务信息发送给后端。当前第一版后端会根据 `workflowId` 返回已有 workflow，后续可以在这个接口内部替换成真正的 EvoFlow 动态执行。

### 操作

```bash
curl -X POST http://127.0.0.1:8000/api/render/run \
  -H "Content-Type: application/json" \
  -d '{"workflowId":"test3","task":"Find hotspots","dataset":"hurricane_sandy_2012_100k_sample.csv","viewType":"Point"}'
```

### 预期结果

返回 Unity-ready render JSON，结构包含：

```json
{
  "visualizationPayload": {
    "views": [...]
  },
  "resultSummary": {...}
}
```

### 通过标准

返回内容中 `visualizationPayload.views[0].points` 存在，当前 `test3` 应包含 69,404 个点和 1,322 个选中点。

---

## 阶段 5：测试 Unity 是否能请求后端

### 目标

确认 Unity 作为桌面前端可以通过 HTTP 访问本地后端。

### 操作

1. 保持后端服务运行。
2. 打开 Unity 项目：

```text
/Users/cyt/Desktop/untiy-project-hkust/unity-agentic-vis-pipeline
```

3. 在场景中新建一个空物体。
4. 挂载脚本：

```text
BackendServiceRenderController
```

5. Inspector 中保持默认配置：

```text
backendBaseUrl = http://127.0.0.1:8000
workflowId = test3
checkHealthBeforeRender = true
renderOnStart = true
useRunEndpoint = false
```

如果要测试任务式 POST 请求，可以改成：

```text
useRunEndpoint = true
taskText = Find concentrated morning pickup hotspots in the Hurricane Sandy sample.
dataset = hurricane_sandy_2012_100k_sample.csv
requestedViewType = Point
```

6. 点击 Play。

### 预期结果

Unity Console 应先显示后端健康检查通过，然后显示已从后端获取 workflow 并尝试渲染。

### 通过标准

Unity Console 没有出现请求失败、连接失败或 JSON 解析失败。

---

## 阶段 6：测试 Unity 是否能渲染点视图

### 目标

确认后端返回的数据能进入现有 Unity 前端渲染链。

### 操作

继续使用阶段 5 的场景配置，运行 Play。

### 预期结果

场景中应出现 `test3` 对应的 point view。

当前数据规模：

```text
总点数：69404
选中点数：1322
视图类型：Point
```

### 通过标准

场景中出现点云/点视图，并且 Unity Console 显示 rendered view 数量大于 0。

### 如果没有显示

优先检查：

1. Console 是否有 IATK 相关报错。
2. `renderRoot` 位置和缩放是否导致点不在相机视野中。
3. `pointSize` 是否太小。
4. 返回 JSON 是否有 `visualizationPayload.views[0].points`。

---

## 阶段 6.5：测试 Unity 命令输入窗口

### 目标

确认桌面 App 形态下，用户可以在 Unity 窗口里输入自然语言命令，点击按钮后请求 EvoFlow 后端，并渲染返回结果。

### 操作

1. 保持后端服务运行：

```bash
cd /Users/cyt/Desktop/untiy-project-hkust/OperatorsDraft
./run_backend_server.sh
```

2. 打开 Unity 项目。
3. 在场景中新建空物体。
4. 挂载脚本：

```text
BackendCommandWindowController
```

5. 点击 Play。
6. Unity 画面左上角应出现 `EvoFlow Command` 窗口。
7. 点击 Dataset 行右侧的 `Load`，确认数据集列表能加载。
8. 选择一个数据集，或保留默认 `hurricane_sandy_2012_100k_sample.csv`。
9. 输入任务命令，例如：

```text
Find concentrated morning pickup hotspots in the Hurricane Sandy sample and render them as a backend-ready point visualization.
```

10. 点击 `Run And Render`.

### 两种模式

默认：

```text
executeEvoFlow = false
```

这会走已有 `test3` workflow，适合快速测试 Unity 通信和渲染。

动态执行：

```text
executeEvoFlow = true
```

这会让后端调用 `run_evoflow.sh`，根据任务生成新的 JSON，再返回给 Unity。该模式更慢，适合在后端稳定后测试。

### 通过标准

- Unity 窗口中状态从 `Ready` 变成请求/渲染状态。
- 后端终端能看到请求日志。
- Unity Console 没有连接失败或 JSON 解析失败。
- 场景中出现 point view。

---

## 阶段 7：测试错误处理

### 目标

确认系统在后端未启动时会给出清晰错误。

### 操作

1. 停止后端服务。
2. 在 Unity 中运行挂有 `BackendServiceRenderController` 的场景。

### 预期结果

Unity Console 应显示类似：

```text
EvoFlow backend health check failed
```

### 通过标准

Unity 不崩溃，并且能明确提示后端连接失败。

### 补充检查

如果后端返回的是标准错误 JSON，而不是可渲染视图 JSON，Unity 现在应在 Console 或命令窗口状态栏中显示类似 `evoflow_execution: ...`、`routing: ...`、`post_request: ...` 的具体错误阶段信息，而不是只显示笼统的解析失败。

---

## 阶段 8：桌面 App 打包前测试

### 目标

确认在打包桌面 App 前，Unity Editor 内的前后端通信链路已经稳定。

### 必须通过的检查

- 后端 `/api/health` 可访问。
- 后端 `/api/render/test3` 可访问。
- Unity 能请求后端。
- Unity 能解析返回 JSON。
- Unity 能渲染 point view。
- Unity 停止 Play 后不会留下异常状态。

### 通过标准

以上全部通过后，再进入桌面 App 打包阶段。

---

## 阶段 9：桌面 App 打包方案测试

### 推荐先测简单版

第一版不让 Unity 自动启动后端，而是手动启动：

```bash
cd OperatorsDraft
./run_backend_server.sh
```

然后运行 Unity 打包出的桌面 App。

### 通过标准

桌面 App 能像 Unity Editor 一样访问：

```text
http://127.0.0.1:8000/api/render/test3
```

并显示对应可视化。

### 后续增强

等手动启动后端的版本稳定后，再考虑：

```text
Unity App 启动时自动拉起本地后端进程
```

这一步需要额外处理 Python 环境、路径、进程生命周期和应用退出清理。

---

## 当前状态记录

截至当前版本：

- 后端服务文件已新增。
- Unity HTTP client 已新增。
- Unity 后端请求渲染控制器已新增。
- `/api/render/test3` 已在本地验证可返回 Unity-ready JSON。
- Unity Editor 端到端渲染仍需实际运行验证。

---

## 阶段 10：桌面 App Bootstrap 测试

### 目标

确认只通过一个 Unity 场景启动器，就可以形成“命令输入 + 本地后端 + 渲染输出”的桌面 App 入口。

### 操作

1. 打开 Unity 项目：

```text
/Users/cyt/Desktop/untiy-project-hkust/unity-agentic-vis-pipeline
```

2. 新建一个空场景。
3. 新建一个空物体。
4. 挂载脚本：

```text
DesktopAgenticAppBootstrap
```

5. 点击 Play。

### 预期结果

- 场景中自动补齐 Camera 和 Directional Light。
- 场景中自动生成 `DesktopBackendServiceController`。
- 左上角出现 `EvoFlow Command` 命令窗口。
- 若本地环境支持 Python，Unity 可尝试自动拉起 `OperatorsDraft` 后端。

### 通过标准

- 不需要手动预先挂多个控制器。
- 命令窗口可点击 `Start Backend` / `Restart Backend` / `Stop Backend`。
- 加载数据集成功后，可以直接点击 `Run And Render`。

### 打包注意事项

Windows build 后，建议先采用如下目录：

```text
YourBuiltApp/
  YourApp.exe
  YourApp_Data/
  OperatorsDraft/
```

如果使用 `StreamingAssets/EvoFlowBackend` 方式，则需要把后端脚本与导出文件一起放入对应目录。
