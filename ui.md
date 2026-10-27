# Lumen UI 方案 ·「传送门」v1.2

> 状态：v1.2（2026-10-24）。已回写 v1.1 复审的 P0 4 / P1 12 / P2 10，并修正 v1.1 自身的 4 处事实错误。
> **效力**：本文是 UI 交互、视觉与实现结构的唯一依据。
> **冲突裁决**：性能预算以 `technoledge.md` §6.10 为准（更严格者优先）；数据布局、Schema、错误码以 `technoledge.md` 为准；其余 UI 细节以本文为准。
> **维护方式**：不划分 A/B 归属；唯一硬约束是 `Launcher.Core` 业务逻辑与 `ILauncherFacade` 契约不被 UI 需求反向改写。

---

## 0. 设计原则

| # | 原则 | 说明 |
|---|---|---|
| P1 | 全屏沉浸 | 启动即全屏；单一返回栈（§1.2） |
| P2 | 世界优先 | 纵深场景承载全部功能，不是背景图 |
| P3 | 文字永远 2D | 文字一律 Avalonia 矢量渲染 |
| P4 | 功能即场景 | 安装/启动/报错成为场景语言 |
| P5 | 永不黑屏 | GL 失败、上下文丢失、TDR 均降级为静态背景 |
| P6 | 可回退 | 经典模式一键切回六页 XAML |

---

## 1. 窗口与输入

### 1.1 窗口状态机（Avalonia 12 实际 API）

> **核实**：`Window.SystemDecorations` 在 12.1.4 仍存在但已标注 `[Obsolete("Use WindowDecorations instead.")]`，
> 项目开启 `TreatWarningsAsErrors`，**使用它会直接构建失败**。必须用 `WindowDecorations`。
> 枚举值核实为 `None / BorderOnly / Full`。

| 状态 | `WindowState` | `WindowDecorations` | `CanResize` |
|---|---|---|---|
| 沉浸全屏（默认） | `FullScreen` | `None` | false |
| 窗口模式 | `Normal` | `Full` | true |
| 最小化 | `Minimized` | 保持退出前设置 | — |

```csharp
void ExitFullScreen()
{
    WindowState = WindowState.Normal;
    WindowDecorations = WindowDecorations.Full;   // 不可停留在 None
    CanResize = true;
    ClampToWorkArea();
}
```

- 多显示器：全屏作用于窗口当前所在屏幕；记住上次屏幕。
- 位置记忆：`AppSettings.RememberWindowPlacement` + `WindowPlacement` 字符串，恢复时钳制到工作区。
- 单实例二次启动：激活已有窗口并置前，不新开进程。

### 1.2 ESC：唯一返回栈

```
1. Modal 打开 → 关闭 Modal
2. 面板打开   → 关闭面板
3. 沉浸全屏   → 退出全屏（恢复 WindowDecorations=Full）
4. 否则       → 打开退出确认 Modal
```

由 `Shell/InputRouter` 统一维护栈，**任何控件不得自行处理 ESC**。

> 经典视图切换：`F2` 或沉浸式左上角「经典视图」按钮；窗口装饰随之切换（沉浸 = `None` + FullScreen + 不可缩放，经典 = `BorderOnly` + 可缩放）。

### 1.3 焦点作用域（修正：消除 Space/Enter/Tab 歧义）

| 作用域 | Tab | Enter | Space |
|---|---|---|---|
| World | 在门之间循环 | 打开选中门的面板 | 启动选中实例 |
| Panel | 在面板内控件循环 | 激活控件 | 切换勾选 |
| Modal | 焦点锁在 Modal 内 | 激活 | 切换勾选 |

- 逐级只有一个活动作用域，由输入路由器维护，面板打开时 World 作用域失活。
- 每扇门有焦点态：**门框描边 + 外发光**，不能只靠颜色区分。
- 无障碍：门的视觉在 GL 里，但键盘与读屏依赖一层**透明的焦点代理控件**（见 §3.2）。

### 1.4 DPI、坐标与单位（修正）

> **核实**：`OnOpenGlRender` 的签名是 `protected OnOpenGlRender(GlInterface gl, int fb)`，**没有 w/h 参数**；
> `RenderScaling` 不会自动进入 GL 回调。尺寸须自行取：`Bounds.Size * (TopLevel.GetTopLevel(this)?.RenderScaling ?? 1)`，
> 或使用 `OpenGlControlBase.GetPixelSize(IPresentationSource)`。

| 量 | 单位 | 说明 |
|---|---|---|
| 世界坐标 | 米（1 unit = 1 m） | 所有布局参数按米写 |
| 面板布局 | DIP | Avalonia 原生 |
| 投影/射线数学 | **DIP** | 统一用 DIP，避免缩放两套换算 |
| GL 视口 / `glReadPixels` | 物理像素 | 仅此两处用物理像素 |

> 换算只在一处发生：屏幕 DIP ↔ 物理像素 `× RenderScaling`，且仅用于 GL 视口与截图。

---

## 2. 视觉令牌

### 2.1 色板（末地紫）

| 令牌 | 色值 | 用途 |
|---|---|---|
| `--bg-deep` | `#0A0612` | 天穹底 |
| `--bg-mid` | `#140B24` | 岛体暗部 |
| `--accent` | `#8B5CF6` | 门框、选中 |
| `--accent-hi` | `#C084FC` | 悬停、焦点 |
| `--accent-cold` | `#6366F1` | 远景、雾 |
| `--text` | `#F3EEFF` | 主文字 |
| `--text-dim` | `#A79BCB` | 次文字 |
| `--danger` | `#F43F5E` | 裂纹、需修复 |
| `--warm` | `#FFB86B` | 火把点缀 |

**颜色单一来源（修正）**：`AppSettings.AccentColor` 是唯一可写来源，默认值改为 `#8B5CF6`；
UI 令牌从它派生（`--accent = AccentColor`、`--accent-hi` 提亮 12%、`--accent-cold` 混蓝），不再另存一份紫色常量。

门内渐变：`#C084FC → #6D28D9 → #1E1B4B`。

### 2.2 动效令牌

| 动作 | 时长 | 缓动 |
|---|---|---|
| Tooltip 淡入/淡出 | 120 / 80ms | `CubicOut` / `Linear` |
| 图标悬停放大 | 160ms | `CubicOut` |
| 面板推近 | 420ms | `CubicInOut` |
| 面板淡入 | 240ms | `CubicOut` |
| 启动推镜 | 600ms | `CubicIn` |
| 启动白闪 | 250ms（峰值 80ms；减少动效 120ms 无峰值） | `Linear` |
| 报错脉冲 | 320ms × 2（减少动效 ×1） | `SineInOut` |
| 镜头公转 | 8s / 圈 | `Linear` |

---

## 3. 渲染架构（修正：门归 GL）

### 3.1 图层

```
L5  ModalLayer       可交互（确认框、登录码、向导）
L4  TooltipLayer     穿透（IsHitTestVisible=false）
L3  PanelLayer       交互面板（布局 + RenderTransform）
L2  PostFxLayer      暗角 / 色差 / 扫描线（压在 GL 之上）
L1  WorldLayer       OpenGlControlBase（门 / 图标 / 岛 / 粒子）
```

### 3.2 谁渲染什么（消除自相矛盾）

| 对象 | 渲染者 | 命中测试 | 键盘/读屏 |
|---|---|---|---|
| 岛、门框、门内、星尘、图标勋章 | **GL** | 世界空间 OBB 射线 | 焦点代理控件 |
| 面板、按钮、输入框、列表 | Avalonia | 原生命中测试 | 原生 |
| Tooltip | Avalonia（穿透） | 无 | 无 |
| Modal | Avalonia（可交互） | 原生 | 原生 |

> v1.1 的「传送门 = CompositionVisual」已废弃。门必须走 GL 才有深度、雾、门内 shader 与光效；
> 合成层无法参与 GL 深度，也会把门内 shader 变成无处安放的摆设。

**焦点代理**：每扇门在上层放一个透明控件 `PortalFocusProxy`
（`Opacity = 0`、`IsHitTestVisible = false`、`Focusable = true`、`IsTabStop = true`），
按门投影矩形定位，设置 `AutomationProperties.Name = "实例 1.21.1 Fabric"`。
它只承担 Tab 焦点与读屏语义，鼠标仍走射线拾取；焦点环由 GL 绘制。

### 3.3 摄像机与投影（修正单位与原点）

```csharp
public sealed class Camera
{
    public Vector3 Position = new(0, 2.2f, 8.5f);   // 米
    public Vector3 Target   = new(0, 0.8f, 0);
    public float   FovY     = 45f;                  // 度
    public float   Near     = 0.1f, Far = 100f;
}
```

面板投影（全 DIP，无矩阵）：

```csharp
// bounds 为面板层尺寸（DIP）
float focalDip = (bounds.Height * 0.5f) / MathF.Tan(cam.FovY * MathF.PI / 360f);
float scale    = focalDip / distance;                  // distance 为米
var p          = ProjectToScreen(cam, worldPos, bounds); // DIP

panel.RenderTransformOrigin = RelativePoint.TopLeft;   // 关键：默认是中心，会导致整体偏移半个面板
panel.RenderTransform =
    new MatrixTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(p.X, p.Y));
```

> 两个修正点：① `RenderTransformOrigin` 默认值为 `50%,50%`，不改会让 `CreateScale*Translate` 偏半个面板；
> ② focal 必须与 `p` 使用同一单位（均 DIP），否则 DPI 变化时面板位置漂移。

### 3.4 文字清晰度

- 面板 `scale ∈ [0.9, 1.1]`；超出范围改**面板尺寸 + 位移**，不靠缩放。
- 面板**始终 billboard 正对镜头，不做倾斜**（v1.1 的"倾斜 ≤15° 仍可读"与仿射变换冲突，已删除）。
- 大面积面板可选 2× 离屏栅格化（`RenderTargetBitmap`，约 +4MB/块），默认关闭。

### 3.5 GL 后端锁定（修正：从"断言"变"配置"）

> **核实**：`Avalonia.Win32PlatformOptions` 在 `Avalonia.Win32` 程序集中，
> `RenderingMode: IReadOnlyList<Win32RenderingMode>`，枚举值 **`Software / AngleEgl / Wgl / Vulkan`**；
> `CompositionMode` 枚举值 **`WinUIComposition / DirectComposition / LowLatencyDxgiSwapChain / RedirectionSurface`**。

```csharp
AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .With(new Win32PlatformOptions
    {
        RenderingMode = new[] { Win32RenderingMode.AngleEgl, Win32RenderingMode.Wgl, Win32RenderingMode.Software },
        CompositionMode = new[] { Win32CompositionMode.WinUIComposition, Win32CompositionMode.RedirectionSurface },
        DpiAwareness = Win32DpiAwareness.PerMonitorDpiAware,
    })
    .StartWithClassicDesktopLifetime(args);
```

- 顺序即优先级：先 ANGLE/EGL，再 WGL（需 `WglProfiles` 指定 `GlVersion`），最后 Software。
- **运行期必须确认实际后端**：`OpenGlControlBase.InitializeAsync` 失败、或拿到的上下文版本 < **GLES 3.0**，
  直接进入降级（静态背景），不发任何 GL 调用。
- Vulkan 不列入：`OpenGlControlBase` 依赖 GL 互操作，Vulkan 路径不提供可用 GL 上下文。

### 3.6 GL API 边界（修正：`Launcher.Render` 不依赖 Avalonia）

`Launcher.Render` 只依赖一个自有接口，**不引用 `Avalonia.OpenGL`**：

```csharp
// Launcher.Render/IGlApi.cs
public interface IGlApi
{
    IntPtr GetProcAddress(string proc);
    string Version { get; }
    string Renderer { get; }
}
```

App 侧用 `Avalonia.OpenGL.Controls.OpenGlControlBase` 提供的 `GlInterface` 适配该接口（`GlInterface.GetProcAddress` 已核实存在）。
这样 `Launcher.Render` 可脱离 Avalonia 做单测（用 mock GL），也不会引入第二套 GL 加载器。

> 命名空间核实：`OpenGlControlBase` 位于 **`Avalonia.OpenGL.Controls`**（不是 `Avalonia.OpenGL`）。

### 3.7 上下文丢失与 TDR（修正：计时条件）

- 主信号：覆写 `OnOpenGlLost()`（已核实存在）。
- 辅助看门狗：仅当**窗口可见且已请求渲染**时计时；失焦/最小化/IDE 断点都会暂停渲染，此时**必须停表**，
  否则会误判为上下文丢失并触发降级。

```
OnOpenGlLost() → 尝试重建 1 次（延迟 1s）
              → 仍失败 → 切静态背景 + 经典 2D
```

> 不新增错误码：错误码表以 `technoledge.md` §8.2 为准，UI 侧只写日志。

### 3.8 数据流

```
ILauncherFacade（Core，业务逻辑不改）
   GetSnapshotAsync / GetTasksAsync / GetRunningSessionsAsync
   InstallAsync / LaunchAsync / IProgress<InstallProgress> / IProgress<LaunchEvent>
        ▼
AppShellViewModel
   ObservableCollection<PortalItem>（每门一态）
   ShellMood（派生）
        ▼
ImmersiveShell ─ WorldView(GL) ─ PortalView/PortalIcon
              ├ PanelLayer(RenderTransform) ├ TooltipLayer ├ ModalLayer
              └ MusicPlayer
```

---

## 4. 状态模型

### 4.1 每门一态

```csharp
public enum PortalState { Idle, Installing, Launching, Running, Broken }

public sealed class PortalItem : ObservableObject
{
    public string InstanceId { get; init; } = "";
    [ObservableProperty] private PortalState _state;
    [ObservableProperty] private double _progressPercent;  // -1 = 未知
    [ObservableProperty] private string? _taskId;
}
```

派生 `ShellMood`（纯函数，可单测）：`any Broken → Alarm`；`any Installing/Running → Busy`；否则 `Calm`。

### 4.2 启动恢复（修正第 4 步）

```
1. GetSnapshotAsync()          → 实例列表（含 DB 中的 state 字段）
2. GetRunningSessionsAsync()   → 置 Running
3. GetTasksAsync()             → 置 Installing + 进度
4. DB state = broken           → 置 Broken
```

> v1.1 的第 4 步用 `GetInstanceAsync` 逐个校验版本目录，但它只读 DB 且会引入 N 次 IO，直接击穿 1.5s 预算。
> **深度校验只在两个时机做**：打开实例面板时、用户点击修复时。

- 轮询：存在非 Idle 状态时每 1s 调一次 `GetTasksAsync` / `GetRunningSessionsAsync`；实时刷新靠 `IProgress`（10Hz 已节流）。
- 取消/重试：`CancelTaskAsync(taskId)` / `RetryTaskAsync(taskId)`。

---

## 5. 面板清单（11 个）

| # | 面板 | 入口（修正：不做"石碑背面"） |
|---|---|---|
| 1 | 实例详情 | 点击门 |
| 2 | 任务中心 | 右上角常驻按钮 + 侧栏图标 |
| 3 | 下载 / 版本库 | 侧边卷轴台 |
| 4 | 账户 | 门旁灵魂灯 |
| 5 | 设置 | 石碑 |
| 6 | 安装向导 | 空态线框门 |
| 7 | 导入向导 | 岛缘裂缝 |
| 8 | 首次启动引导 | 开场自动 |
| 9 | 更新入口 | 天际流星 + 设置内条目 |
| 10 | 诊断 / 完整性阻断 | `E8002` 时自动弹出；平时在设置里有入口 |
| 11 | 关于 / 许可 / 第三方声明 | **设置面板内独立条目** |

- 日志页固定跳经典 2D（长文本、复制、滚动同步成本过高）。
- 「石碑背面」这种隐藏入口取消，全部改为**设置内可见条目**，否则用户找不到。

---

## 6. 设置项

```csharp
// AppSettings 追加
public bool       ClassicMode { get; init; } = false;
public bool       Enable3DHero { get; init; } = true;
public bool       EnableEnhanced3D { get; init; } = false;   // 增强 3D（OpenGL 装饰层），实验性
public bool       ReduceMotion { get; init; } = false;
public WindowMode WindowMode { get; init; } = WindowMode.FullScreen;
public bool       RememberWindowPlacement { get; init; } = true;
public bool       MusicEnabled { get; init; } = true;
public double     MusicVolume { get; init; } = 0.4;
public string     WindowPlacement { get; init; } = "";
// AppSettingsPatch 追加同名可空参数；AccentColor 默认值改为 #8B5CF6
```

- 旧 `launcher.json` 缺字段走默认值，无需 schema 升级。
- 仅 DTO 与存取改动，不触碰 Core 业务逻辑。

---

## 7. 阶段规划

### S1 · 世界层骨架

**首帧关键路径（新增，必须照此实现）**

| 时间窗 | 动作 |
|---|---|
| 0–150ms | 进程启动、配置加载、DB 打开、单实例锁 |
| 150–300ms | **先显示 2D 骨架**（纯色背景 + 侧栏 + 加载指示），不等待 3D |
| 300ms+ | 异步并行：完整性快校验、manifest 缓存、GL 初始化、音频设备初始化 |
| ≤1500ms | 世界层首帧可见（岛 + 门） |

| # | 任务 |
|---|---|
| 1 | `Program.cs` 按 §3.5 配置 `Win32PlatformOptions`（锁定 ANGLE→WGL→Software 顺序） |
| 2 | `ImmersiveShell` 五层容器；`WindowDecorations` 状态机（§1.1） |
| 3 | `InputRouter`：ESC 返回栈 + 三个焦点作用域（§1.2/1.3） |
| 4 | `CameraController`：默认参数见 §3.3，公转 8s、视差 ±2°、缓动 0.08 |
| 5 | `WorldView : Avalonia.OpenGL.Controls.OpenGlControlBase`，覆写 `OnOpenGlInit/OnOpenGlRender/OnOpenGlLost/OnOpenGlDeinit` |
| 6 | GL 初始化后检查版本 ≥ GLES 3.0，否则直接降级 |
| 7 | `PortalLayout`（纯函数）+ 门/岛/雾/天穹 GPU 资源 |
| 8 | 失焦/最小化：停渲染、降帧；**同时停看门狗计时** |
| 9 | `LUMEN_DISABLE_GL=1` + 帧时间采样 |
| 10 | 移除对 `FakeLauncherFacade` 的静默回退：Core 初始化失败必须显示错误 + 诊断入口（仅 Debug 或 `--fake` 允许 Fake） |

**验收**

| 项 | 标准 |
|---|---|
| 全屏 | 启动全屏；ESC 退出后窗口**可拖动、有关闭按钮**；F11 可切 |
| 后端 | 启动日志打印实际渲染后端与 GL 版本；非 GLES3+ 自动降级 |
| 场景 | 1080p 平均 60fps、1% low ≥ 50fps（60s 采样） |
| 数据 | 3 实例 → 3 门，符合 §8.1 |
| 首帧 | 2D 骨架 ≤ 300ms；世界层 ≤ 1.5s |
| 降级 | `LUMEN_DISABLE_GL=1` → 静态背景 + 经典 2D，不崩不黑 |
| 失焦 | 切窗后 CPU ≤ 1%，且不误判上下文丢失 |
| Core 失败 | 不静默回退 Fake，显示错误并可导出诊断 |
| 构建 | 0 警告 0 错误（含 `TreatWarningsAsErrors`） |

### S2 · 命中与传送门交互

| # | 任务 |
|---|---|
| 1 | **Spike**：GL 场景 + 上层 `RenderTransform` 面板，验证命中测试与 `ClipToBounds=false` |
| 2 | `RayPicker`：DIP 屏幕 → 世界射线 → **OBB** 相交（门有 22° 旋转） |
| 3 | 图标勋章：GL billboard quad，`Orientation` 用 CPU 算（面向相机）；纹理 ≤128×128，按哈希缓存，缺失回退首字色块 |
| 4 | `PortalFocusProxy`：透明可聚焦控件，提供 Tab 焦点与读屏语义（§3.2） |
| 5 | 悬停态：图标 ×1.15、门框描边、星尘加速 |
| 6 | `TooltipLayer`（穿透）：实例名 + `1.21.1 · Fabric 0.20.1 · 3 天前`，偏移 `(+16,-18)`，边缘翻转 |
| 7 | 折叠：> 7 扇门 → 第 7 位「更多」，列表虚拟化 |

**验收**

| 项 | 标准 |
|---|---|
| 命中 | 门中心/边缘/背面均可判对；DPI 100%/150% 一致 |
| 悬停延迟 | 进入 OBB 到开始放大 ≤ 80ms |
| billboard | 任意镜头角度图标无侧向压扁 |
| 键盘 | Tab 能遍历门并显示焦点环；Enter 打开；Space 启动 |
| 读屏 | 焦点代理控件可被无障碍树枚举，名称正确 |
| 折叠 | 8 实例 = 6 门 + 1「更多」；展开 50 项 ≤ 200ms |

### S3 · 状态模型与恢复

任务与验收同 v1.1，但恢复改为 §4.2（读 DB state，不做逐实例深校验）。

| 验收 | 标准 |
|---|---|
| 恢复 | 安装中强杀 → 重启后仍 Installing，进度误差 ≤ 5% |
| 恢复 | 游戏运行中强杀启动器 → 重启后 Running |
| 并发 | 2 安装 + 1 运行 + 1 损坏同时表达，互不覆盖 |
| 取消 | 任务中心取消后 3s 内回 Idle |
| 单测 | 派生函数与状态映射 100% 覆盖 |

### S4 · 面板系统（11 个）

| # | 任务 |
|---|---|
| 1 | `PanelHost`：DIP 投影 + `RenderTransformOrigin=TopLeft`（§3.3） |
| 2 | `scale ∈ [0.9,1.1]`，超出改尺寸；**面板始终正对镜头** |
| 3 | 进出动画 420ms + 240ms |
| 4 | 11 面板接入（§5） |
| 5 | `ModalLayer` 承载确认框/登录码/向导 |
| 6 | 经典模式切换（无需重启） |
| 7 | 16:9 / 4:3 / 窗口 960×640 边界约束 |
| 8 | `AvaloniaUseCompiledBindingsByDefault = true` |

**验收**

| 项 | 标准 |
|---|---|
| 可点击 | 11 面板按钮/输入框全部可点（含 960×640） |
| 清晰 | `scale ∈ [0.9,1.1]` 且无倾斜时正文清晰 |
| 定位 | 125%/150% DPI 下面板中心偏差 ≤ 2 DIP |
| 遮挡 | 近面板覆盖远面板，无穿模 |
| 动画 | 进出 ≤ 700ms，无跳变 |
| 覆盖 | 11 个面板全部存在且入口可见 |
| 经典模式 | 一键切换，功能等价 |
| 输入 | 中文 IME 正常 |

### S5 · 音频

同 v1.1（NAudio 3.0.1 + 显式 `NAudio.Wasapi` + `NAudio.Vorbis 3.0.0`，自产合成，2 首 × 4 分钟）。

**新增验收**：音频设备初始化失败（无默认设备/被独占）→ 静默降级为无声，UI 不阻塞、不崩；
下一次窗口激活时重试一次，仍失败则本次会话不再重试。

### S6 · 硬化与宣传

| # | 任务 |
|---|---|
| 1 | 降级矩阵（§9.2） |
| 2 | 帧率自适应：3s < 30fps → 关粒子 → 降分辨率 → 静态背景 |
| 3 | **截图链路**：见 §7.1 |
| 4 | `--demo` 演示脚本（真实数据，不脱敏） |
| 5 | 关于/许可/第三方声明入口（设置内） |
| 6 | 性能回归脚本 |
| 7 | CI 增加 GLSL 静态检查（`glslangValidator --target-env opengl`，校验全部 `#version 300 es` 着色器） |

### 7.1 截图链路（修正：不能用 RenderTargetBitmap 抓 GL）

> 事实：`OpenGlControlBase` 把 GL 画面挂成 composition child visual，`RenderTargetBitmap` 走软件渲染，
> 官方文档明确 GPU 内容"可能无法正确渲染"。因此 2560×1440 截图必须自己抓像素。

```
1. 暂停动画，创建 2560×1440 离屏 FBO，按目标宽高比重算 Camera
2. 渲染世界层到 FBO
3. glReadPixels → BGRA 缓冲（GlInterface 未暴露时用 GetProcAddress 取函数指针）
4. 渲染 2D 层（面板/Tooltip/Modal）到 RenderTargetBitmap
5. 手动 alpha 合成（BGRA，注意预乘 alpha）
6. 编码 PNG 落盘
```

- 截图期间固定镜头与状态，保证可复现。
- GL 已降级时：只导出 2D 层，并在文件名与界面提示"无世界层"。

**验收**

| 项 | 标准 |
|---|---|
| 分辨率 | 导出 2560×1440 / 1080×1080 像素精确 |
| 完整性 | 世界层与面板层均出现，无黑块、无缺失 |
| 一致性 | 与屏幕构图一致（相机与状态固定） |
| 降级 | GL 关闭时导出 2D 层并给出提示 |

---

## 8. 布局与视觉

### 8.1 门布局

| 规则 | 参数 |
|---|---|
| 主门 | `(0,0,0)` 正对镜头 |
| 其余门 | 弧线间隔 30°（`ArcStepRadians`），半径 3.2 m（`ArcRadius`），z 递减 0.55 m（`DepthStep`） |
| 缩放 | `0.80^(|k|/2)`，`k` 为左右交替的带符号位次 |
| 不透明度 | `clamp(0.9 - 0.10*|k|, 0.35, 0.9)` |
| 折叠 | > 7 扇时第 7 位为「更多」 |

> 左右位次使用 `(i+1)/2`，实测存在 0.5 的左右偏移（第 2/3 扇不对称），已记录为 S2 前待修缺陷。

### 8.2 门结构

| 部件 | 实现 |
|---|---|
| 门框 | 4 个拉伸立方体（GL，程序化） |
| 门内 | 半透明四边形 + 流动噪声 shader（GLSL ES 3.0） |
| 图标 | GL billboard quad，纹理 ≤128×128 |
| 状态环 | 弧长 = 进度；`Percent=-1` 无终点旋转 |

### 8.3 GL 资源与状态

- 资源（VAO/VBO/纹理/程序）在 `OnOpenGlInit` 创建，`OnOpenGlDeinit` 释放。
- 上下文重建后必须**全部重放**（资源句柄已失效）。
- 每帧只更新必要 uniform，不在渲染线程分配内存。

---

## 9. 性能与降级

### 9.1 预算

| 指标 | 目标 | 当前实测 | 是否达标 |
|---|---|---|---|
| 冷启动到世界层首帧 | ≤ 1.5s | 5.2–8.1s（开发态 / ANGLE / 未 R2R / 沙箱内） | ❌ 未达标 |
| 交互中帧率 | 60fps，1% low ≥ 50 | 未测（帧时间采样要求窗口聚焦且样本 ≥ 120 帧） | — 未测 |
| 空闲帧率 | 上限 30fps（无输入 5s 后） | 未测 | — 未测 |
| 空闲 CPU | < 5% | 未测 | — 未测 |
| UI 空闲内存 | < 150MB（technoledge §6.10） | 未测 | — 未测 |
| 包体 | Portable.zip ≤ 96MB；publish 增量 ≤ +35MB | Portable.zip 60.9MB（60,918,322 B）；Setup.exe 68.4MB（68,433,392 B）；publish 242 文件 | ✅ Portable.zip 达标；publish 增量未测 |
| 显存 | 不设指标，仅记录 GPU 名称 | Intel Arc 140T（ANGLE 2.1.1，OpenGL ES 3.0） | — 无指标 |

> 首帧实测口径：开发态直接运行（非 R2R publish、无预热缓存），且运行于沙箱内。R2R publish + 冷/热缓存条件下的复测**仍待执行**，达标结论未成立。

### 9.2 降级矩阵（全部真实 API）

| 场景 | 判定 | 行为 |
|---|---|---|
| 远程桌面 | `GetSystemMetrics(SM_REMOTESESSION=0x1000)` | 静态背景 + 关粒子与后处理 |
| GL 初始化失败/软件后端 | 初始化异常 或 后端为 Software | 静态背景，**不承诺帧率** |
| GLES < 3.0 | 版本检查 | 直接降级 |
| 上下文丢失 | `OnOpenGlLost()` 或可见期看门狗 | 重建 1 次 → 静态背景 |
| 物理内存 < 4GB | `GlobalMemoryStatusEx` | 粒子减半、关雾 |
| 减少动效 | `ReduceMotion` | 关公转/粒子，白闪与脉冲降级 |
| 连续低帧 | 3s < 30fps | 逐级降级至静态背景 |

---

## 10. 目录与依赖

```
src/Launcher.Render/        # 不引用 Avalonia，依赖 IGlApi（§3.6）；当前为扁平文件，无子目录
├─ IGlApi.cs  Gl.cs  Camera.cs  Math.cs
├─ MeshData.cs  PortalLayout.cs  SceneRenderer.cs
├─ Shaders.cs  BlockModelLoader.cs
└─ Launcher.Render.csproj
src/Launcher.App/           # 根目录另有 Program.cs / App.axaml(.cs)
├─ Shell/  World/  Views/  ViewModels/  Services/  Mock/
└─ Launcher.App.csproj
tools/audio/                # 计划（S5），尚未创建
```

| 包 | 版本 | 用途 | 状态 |
|---|---|---|---|
| `StbImageSharp` | 2.27.11 | 贴图解码 | 已引入（App + Render） |
| `SharpGLTF.Core` | 1.0.5 | GLB 解析（S1 暂不引 `.Toolkit`） | 计划（S4/S5，未引入） |
| `NAudio` + `NAudio.Wasapi` | 3.0.1 | `WasapiOut` | 计划（S4/S5，未引入） |
| `NAudio.Vorbis` | 3.0.0 | Ogg 解码 | 计划（S4/S5，未引入） |

其余已引入包以 `Directory.Packages.props` 为准，并登记 `THIRD_PARTY_NOTICES.md`。新增包统一进 `Directory.Packages.props`；不引入 `Silk.NET.OpenGL`。

---

## 11. 维护方式

不划分 A/B 归属；`Launcher.Render`、`App` UI、音频资产由同一维护者推进。
唯一硬约束：`Launcher.Core` 业务逻辑与 `ILauncherFacade` 契约不被 UI 反向改写。

---

## 12. 测试与可测性

| 目标 | 用例 |
|---|---|
| `PortalLayout` | 0/1/3/7/8 实例、opacity clamp、折叠边界 |
| `RayPicker` | 正对/旋转 22°/DPI 100%·150%/背面不命中 |
| `CameraController` | 公转幅度、视差上限、失焦冻结 |
| `SceneStateMachine` | 四种组合派生、恢复映射 |
| `SettingsPatch` | 非 null 覆盖、缺字段默认值 |
| `GlApiMock` | `Launcher.Render` 在无 Avalonia 情况下可单测 |

埋点：`LUMEN_DISABLE_GL=1`、`--demo`、`--perf-log`（帧时间 CSV）。

---

## 13. 风险与回退

| 风险 | 回退 |
|---|---|
| GL 后端为 Vulkan/Software | 直接降级静态背景 |
| 上下文丢失/驱动更新 | `OnOpenGlLost` 重建一次 → 降级 |
| 截图抓不到 GL | 已改为 FBO + `glReadPixels`，不依赖 RTB |
| 面板 DPI 偏移 | 统一 DIP + `TopLeft` 原点 |
| 低端机性能 | 逐级降级 + 经典模式 |
| 3D 导航难理解 | 首启引导 + 经典模式 |

---

## 14. 未决项

1. 门是否允许拖动重排？
2. 「更多」展开为弧形还是平面列表？
3. 经典模式是否首启默认？
4. 是否需要 4:3 / 9:16 安全区？
   **结论（2026-10-25）**：4:3 安全区按 S4 任务 7 要求保留；9:16 竖版在 S6 单独裁剪，不进入 v1 验收。

---

## 15. 本轮源码核实结论（证据）

| 断言 | 核实结果 |
|---|---|
| `SystemDecorations` 在 Avalonia 12 不存在 | **部分错误**：属性存在，但标记 `[Obsolete("Use WindowDecorations instead.")]`；项目开启 `TreatWarningsAsErrors` 会**构建失败**，须改用 `WindowDecorations` |
| `WindowDecorations` 枚举 | ✔ 实际值 `None / BorderOnly / Full` |
| `OnOpenGlRender(GlInterface gl, int w, int h, int frame)` | **错误**：实际为 `OnOpenGlRender(GlInterface gl, int fb)`，无尺寸参数 |
| `ctx.IsLost` | **错误**：无 `GlContext` 类型，上下文丢失用 `OnOpenGlLost()` |
| `OpenGlControlBase` 命名空间 | `Avalonia.OpenGL.Controls`（非 `Avalonia.OpenGL`） |
| `TryCreateCompatibleGlContextAsync` | 存在等价能力，但出口是 `InitializeAsync()` + `GetPixelSize()` |
| Win32 后端 | ✔ `Avalonia.Win32PlatformOptions.RenderingMode`，值 `Software/AngleEgl/Wgl/Vulkan`；`CompositionMode` 值 `WinUIComposition/DirectComposition/LowLatencyDxgiSwapChain/RedirectionSurface` |
| `GlInterface.GetProcAddress` | ✔ 存在，可用于 `glReadPixels` 等未暴露函数 |

---

## 16. 文档同步待办

| 项 | 位置 | 动作 | 状态 |
|---|---|---|---|
| 拼写错误 `technoledg.md` | 本文 v1.0/v1.1 遗留 | 已全部改为 `technoledge.md` | 已完成（2026-10-25） |
| A/B 分工条款失效 | `technoledge.md` §14 协作约定 | `technoledge.md` §14 末尾已注明「分工取消」，由本文 §11 取代 | 已完成（2026-10-25） |
| `EnableEnhanced3D` 注释提"B 层" | 本文 | 已改为"增强 3D" | 已完成（2026-10-25） |
| `E9001` 未进错误码表 | 本文 | 已删除该错误码，UI 只写日志 | 已完成（2026-10-25） |
| `AccentColor` 与紫色令牌双源 | §2.1 | 已统一为单一来源 | 已完成（2026-10-25） |
| `AvaloniaUseCompiledBindingsByDefault=false` | [Launcher.App.csproj](file:///D:/启动器/src/Launcher.App/Launcher.App.csproj#L7) | S4 任务 8 改为 `true` | 待办（当前仍为 `false`） |
