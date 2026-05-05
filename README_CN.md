# EvoVis Studio

EvoVis Studio 是一个面向桌面端的智能可视化项目，连接了：

- 自然语言任务输入
- 后端工作流搜索与执行
- 基于 JSON 的结果交换
- Unity 前端交互式可视化渲染

本仓库是一个独立的项目级代码库，包含系统两侧：

- Unity 桌面可视化前端
- EvoFlow 风格后端运行时与本地服务

`unity-agentic-vis-pipeline/` 和 `OperatorsDraft/` 是本仓库中的普通目录，不是 submodule。

当前目标不是浏览器应用，而是桌面应用工作流：Unity 作为可视化前端，后端在本地提供工作流生成、执行和渲染 JSON。

## 项目来源

EvoVis Studio 整合自两个早期项目部分：

- 后端基础：[`cyt2023/evoflow-vis-runtime`](https://github.com/cyt2023/evoflow-vis-runtime)
- Unity 前端基础：[`cyt2023/unity-agentic-vis-pipeline`](https://github.com/cyt2023/unity-agentic-vis-pipeline)

本仓库把两部分合并成一个可克隆、可测试、可交付的项目。

## 项目目标

核心流程是：

```text
用户自然语言命令 -> 后端工作流生成/执行 -> workflow/render JSON -> Unity 渲染
```

用户可以用自然语言描述可视化任务，后端解析任务并组合或执行合适的算子工作流，然后 Unity 根据后端返回的 JSON 渲染结果。

当前重点包括：

- agentic workflow 执行
- OD 出租车行程数据可视化
- 点图、OD link、Space-Time Cube、2D projection 等视图
- Unity 桌面前端与本地后端服务集成

## 仓库结构

### `unity-agentic-vis-pipeline/`

Unity 桌面前端项目。

主要内容：

- Unity 工程文件：`Assets/`、`Packages/`、`ProjectSettings/`
- 适配后的可视化前端
- JSON 驱动的渲染集成代码
- 本地后端服务客户端和控制器
- 项目日志和前端文档

重要入口：

- `Assets/Scripts/Agentic/Unity/`
- `Assets/Scripts/Integration/`
- `Docs/Workspace/`
- `Assets/ProjectLogs/`

### `OperatorsDraft/`

后端项目。

主要内容：

- EvoFlow 风格工作流搜索逻辑
- 后端算子定义和运行时代码
- 本地 HTTP 后端服务
- demo 数据集
- Unity-facing JSON export
- 后端文档和研究笔记

重要入口：

- `evoflow/`
- `operators/`
- `OperatorRunner/`
- `server.py`
- `run_evoflow.sh`
- `run_backend_server.sh`
- `Docs/`

## 系统架构

当前推荐架构：

```text
Unity 桌面应用 -> 本地 EvoFlow 后端服务 -> workflow/render JSON -> Unity renderer
```

更具体地说：

1. 用户输入自然语言任务。
2. 后端选择或执行工作流。
3. 后端返回结构化 JSON。
4. Unity 读取结果。
5. Unity 把 JSON 映射成可渲染视图。
6. Unity 渲染点图、OD link、STC 或 2D projection。

Unity 请求时通常应保持 `viewType = Auto`。后端会根据自然语言任务推断主视图类型，并在返回给 Unity 前整理成 Unity-ready render JSON：

- `Point` 和 hotspot 请求渲染为平面点层。
- `Link` 请求渲染 OD 起终点连线。
- `STC` 请求在返回的 `z` 坐标中使用归一化时间高度。
- `Projection2D` 请求渲染平面投影点图。

## 桌面应用打包

Windows 桌面构建时，把 Unity 前端和本地 EvoFlow 后端一起打包：

```powershell
powershell -ExecutionPolicy Bypass -File scripts\prepare_unity_backend_bundle.ps1
```

该脚本会把 `OperatorsDraft/` 复制到：

```text
unity-agentic-vis-pipeline/Assets/StreamingAssets/EvoFlowBackend
```

并排除本地密钥、缓存和构建目录。

打包前请在目标机器设置 DashScope 环境变量：

```powershell
[Environment]::SetEnvironmentVariable("DASHSCOPE_API_KEY", "your_sk_api_key", [EnvironmentVariableTarget]::User)
[Environment]::SetEnvironmentVariable("DASHSCOPE_MODEL", "qwen-turbo", [EnvironmentVariableTarget]::User)
```

设置后重启 Unity 或桌面应用。真实 API key 不应写入源码仓库。

## 前后端职责

### 后端职责

- 理解或接收自然语言任务
- 选择或执行算子工作流
- 读取数据集
- 生成 workflow/render 结果
- 导出 Unity-facing JSON
- 在桌面模式下通过本地 HTTP 服务返回结果

### Unity 职责

- 请求后端数据或 render JSON
- 解析并验证返回结果
- 按 JSON contract 映射为 Unity render model
- 根据 `viewType` 派发到对应 renderer
- 展示最终可视化结果

## 当前运行方式

### 1. Export-first 路径

后端先生成 JSON，Unity 后续读取该结果。

适合：

- 调试 export 结构
- 验证 schema 和 render payload
- 用稳定样例测试 Unity 渲染

### 2. 本地后端服务路径

Unity 作为桌面前端，按需请求本地后端服务。

适合：

- 自然语言命令驱动的可视化
- 桌面应用交互
- 前后端集成测试

本仓库当前主要优化第二种路径。

## 快速开始

### 从 Unity 前端开始

1. 用 Unity 打开 `unity-agentic-vis-pipeline/`。
2. 打开桌面应用场景。
3. 保持命令窗口中的 view type 为 `Auto`。
4. 进入 Play Mode。
5. 输入自然语言可视化命令。
6. 在 Console 中确认后端返回的 primary view type。

相关文档：

- `unity-agentic-vis-pipeline/README.md`
- `unity-agentic-vis-pipeline/Docs/Workspace/DESKTOP_APP_RUNTIME_CN.md`
- `unity-agentic-vis-pipeline/Docs/Workspace/TESTING_STAGES_CN.md`

### 从后端开始

1. 进入 `OperatorsDraft/`。
2. 启动搜索/export pipeline 或本地服务。
3. 检查生成的 JSON 或通过服务提供给 Unity。

常用文件：

- `OperatorsDraft/run_evoflow.sh`
- `OperatorsDraft/run_backend_server.sh`
- `OperatorsDraft/server.py`
- `OperatorsDraft/README.md`

## 手动测试命令

下面这些命令可直接粘贴到 Unity 命令窗口中测试。

### Point / Hotspot

```text
Show taxi dropoff hotspots as a point visualization. Use dropoff longitude and latitude and highlight concentrated destination areas.
```

### Link

```text
Render taxi trips as origin-destination links. Draw lines from pickup locations to dropoff locations and show movement patterns across the city.
```

### STC

```text
Show all taxi trips in a space-time cube. Use pickup longitude and latitude on the ground plane and pickup time on the vertical axis. Do not filter rows.
```

### 3D Point

```text
Show all taxi pickup points as a 3D point visualization. Use pickup longitude and latitude on the ground plane, and use fare amount or trip distance as height. Do not filter rows.
```

## 测试

后端 contract 测试：

```powershell
cd OperatorsDraft
python -m unittest tests.test_server_contract
```

当前测试覆盖：

- `Auto` 请求保留 EvoFlow 推断的 view type
- 显式 view type override
- 2D projection 平面几何
- STC 归一化时间高度
- Link 起终点 index 适配
- 原始经纬度与归一化坐标混合时的 Unity-ready 坐标修正

## 当前能力

本仓库目前支持：

- JSON 驱动的可视化执行
- 本地后端服务通信
- 桌面 runtime bootstrap 和后端自动启动
- Unity 侧 point / link / STC / 2D projection 渲染派发
- Unity-ready render JSON contract
- EvoFlow 后端 workflow search 和 export
- 继承自 TaxiVis 思路的 OD 与 STC 可视化概念

