# 桌面 App 运行说明

本文档说明当前 `unity-agentic-vis-pipeline + OperatorsDraft` 如何作为本地桌面 App 运行。

## 目标运行方式

目标是让 Unity 不只是读取一个静态 JSON，而是作为桌面前端，接受用户命令，调用本地 EvoFlow 后端，再把返回的结果渲染出来。

运行链路如下：

```text
Unity 桌面 App
  -> DesktopBackendServiceController 自动检查/拉起本地后端
  -> BackendCommandWindowController 接收命令输入
  -> 向 EvoFlow 后端发送 workflow/render 请求
  -> Unity 解析返回 JSON
  -> 现有渲染链输出 Point / STC / Link / 2D Projection
```

## 推荐场景入口

最简单的做法：

1. 在场景中新建一个空物体。
2. 挂载 `DesktopAgenticAppBootstrap`。
3. 点击 Play。

或者直接使用 Unity Editor 菜单：

```text
Tools/ImmersiveTaxiVis/Create Desktop Agentic App Scene
Tools/ImmersiveTaxiVis/Open Desktop Agentic App Scene
```

这个 bootstrap 会自动补齐：
- Camera
- Directional Light
- `DesktopBackendServiceController`
- `BackendCommandWindowController`

## 关键脚本

- `Assets/Scripts/Agentic/Unity/DesktopAgenticAppBootstrap.cs`
  场景快速启动器。

- `Assets/Scripts/Agentic/Unity/DesktopBackendServiceController.cs`
  本地后端进程控制器。负责解析 OperatorsDraft 路径、拉起后端、轮询健康检查、停止已拥有的进程。

- `Assets/Scripts/Agentic/Unity/BackendCommandWindowController.cs`
  App 内命令窗口。可输入任务、选择数据集、决定是否执行 EvoFlow，并直接触发渲染。

- `Assets/Scripts/Agentic/Unity/BackendServiceRenderController.cs`
  更偏自动化演示模式，适合固定 workflow 的自动加载与渲染。

- `Assets/Scripts/Integration/Editor/FrontendDemoSceneCreator.cs`
  Unity Editor 下的一键场景创建工具，可直接生成桌面 App 入口场景。

## 路径约定

当前工作区默认结构：

```text
untiy-project-hkust/
  OperatorsDraft/
  unity-agentic-vis-pipeline/
```

当前桌面 App 控制器会优先尝试这些位置：

1. Inspector 手动指定的 `backendRootOverride`
2. `StreamingAssets/EvoFlowBackend`
3. Unity 项目同级目录中的 `OperatorsDraft`
4. Unity 项目父级目录中的 `OperatorsDraft`

只要找到包含 `server.py` 的目录，就会尝试启动。

## Windows 启动支持

已新增：

```text
OperatorsDraft/run_backend_server.bat
```

因此在 Windows standalone / Editor 中，只要 `python` 或 `py -3` 可用，Unity 就可以尝试自动拉起后端。

## 当前已经做到的程度

目前已经具备：

- Unity 自动检查本地后端是否可用
- 本地后端未运行时，由 Unity 尝试拉起
- 后端健康检查通过后，再发送请求
- Unity 内命令窗口可启动/重启/停止后端
- Unity 内命令窗口会显示 endpoint、已解析 backend 根目录和目录布局提示
- Unity 内命令窗口提供 cached test3 / dynamic point 两种快速 preset
- Unity 前端请求 `workflow/render` JSON 并交给现有渲染链

## 当前还需要实测确认的部分

还需要在目标机器上最终确认：

- Unity Editor 中的真实点云显示效果
- STC / Link / Projection 多视图的场景布局
- 打包后 App 与 `OperatorsDraft` 的相对目录是否保持一致
- 打包环境中的 Python 可执行路径是否可用

## 推荐下一步测试顺序

1. 在 Unity 场景里只挂 `DesktopAgenticAppBootstrap`。
2. Play 后确认左上角命令窗口出现。
3. 点击 `Start Backend`，确认状态变为 backend ready。
4. 点击 `Load`，确认数据集列表加载成功。
5. 使用默认 `test3` 先跑通 cached workflow。
6. 再打开 `Execute EvoFlow`，测试动态执行。
7. 最后再做 Windows build，确认 build 目录旁边放 `OperatorsDraft` 时可以正常工作。
