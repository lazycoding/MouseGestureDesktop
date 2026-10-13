# AGENTS.md

本文件为在此仓库工作的编码代理提供指引。

## 项目概述

MouseGestureDesktop 是一个 **Windows 系统托盘应用**：按住鼠标侧键（XButton1/2）并水平拖动，即可连续切换 Windows 虚拟桌面；轨迹过程中可绘制覆盖层。无侧键手势时，原有的单击/双击行为被完整保留。

- 语言 / 框架：C# · .NET 10 · Windows Forms（`net10.0-windows`）
- 运行形态：无窗口的托盘程序（`WinExe`），单实例（全局 Mutex）
- 平台：仅限 Windows（依赖低级鼠标钩子与虚拟桌面 COM 接口）

## 目录结构

- `MouseGestureDesktop/` — 主程序
  - `Program.cs` — 入口，单实例守卫
  - `App/` — 托盘上下文、手势总控、开机自启管理
  - `Gesture/` — **纯手势状态机**（无 Win32 调用，可单测）
  - `Hooks/` — 低级鼠标钩子（`WH_MOUSE_LL`）
  - `Input/` — 合成点击/按键注入
  - `Desktop/` — 虚拟桌面切换器（COM 内部 API + 键盘快捷键回退）
  - `Overlay/` — 手势轨迹覆盖窗口与渲染
  - `Config/` — 配置模型与 JSON 持久化
  - `UI/` — 设置窗口
- `MouseGestureDesktop.Tests/` — xUnit 测试
- `MouseGestureDesktop.slnx` — 解决方案文件（新版 XML 格式）

## 构建与测试

```bash
dotnet build MouseGestureDesktop.slnx          # 构建整个解决方案
dotnet test                                    # 运行 xUnit 测试
dotnet run --project MouseGestureDesktop       # 本地运行托盘应用（需 Windows）
```

改动后请至少执行 `dotnet build`；涉及手势逻辑时务必跑 `dotnet test`。

## 架构要点

数据流：`LowLevelMouseHook` → `GestureController.OnMouseEvent` → `GestureStateMachine` 产出 `GestureAction` → 控制器据此驱动 `IVirtualDesktopSwitcher`、`InputSynthesizer`、`GestureOverlayWindow`。

- **`GestureStateMachine` 必须保持纯净**：只消费坐标/时间戳事件、只返回 `GestureAction`，不得引入任何 Win32 或 UI 调用——这是其可单测的前提。所有副作用留给 `GestureController`。
- 状态机生命周期：`Idle → Pending →（水平位移超阈值）→ Gesturing`；未触发步进的释放回退为点击，已步进则吞掉点击。
- **钩子回调需快速返回**：副作用通过 `_overlay.BeginInvoke(...)` 延迟到 UI 线程执行，避免低级钩子超时被系统摘除。
- 永不响应自身注入的输入（`e.Injected`）。
- 虚拟桌面切换：`Auto` 模式优先 COM 内部接口，首次失败后 `FallbackSwitcher` 永久降级到键盘快捷键。

## 配置

- 持久化路径：`%APPDATA%\MouseGestureDesktop\config.json`
- 模型见 `Config/GestureConfig.cs`；枚举以字符串形式序列化（`JsonStringEnumConverter`）
- 读取损坏/缺失配置时回退到默认值，不得让托盘应用崩溃

## 代码约定

- 启用 `Nullable` 与 `ImplicitUsings`——遵循现有可空注解，命名空间内引用保持精简
- 文件作用域命名空间（`namespace X;`）
- 类型多为 `sealed`；注释密度参照现有代码：用简短注释解释“为什么”，而非“做什么”
- UI 面向用户的字符串使用中文；代码注释使用英文（与现状一致）
- 新增手势/状态行为时，在 `MouseGestureDesktop.Tests` 补充对应 `[Fact]`，用合成事件序列覆盖
