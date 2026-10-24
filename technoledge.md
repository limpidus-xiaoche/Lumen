# technoledge.md — 启动器项目技术与协作规范

> 面向两名并行开发者。本文是**唯一事实来源**（single source of truth）：接口、目录归属、约定以本文为准；实现与本文冲突时，先改本文再改代码。

| 项目代号 | **`Lumen`（已确认）** |
|---|---|
| 文档版本 | **v0.5** — 复审回写：CmlLib 边界、协作身份、许可存证、编码/源/任务/SSRF 等 |
| 日期 | 2026-10-24 |
| 目标平台 | **v1 = Windows 10/11 x64 only**；跨平台列入 v2 非目标 |
| 开发者 | A：核心（Core / CLI / 打包）　B：应用（Avalonia UI / 交互 / 生态页面） |
| 参考实现 | PCL2（.NET 单文件 + JavaWrapper）、HMCL（Java 继承合并 + Java 约束）、VersePC（Electron + Node 后端 + 插件） |

---

## 0. 怎么用这份文档

1. 开工前两人一起读完 §1–§6，确认接口契约（§5）。
2. 进度只在 **`README.md`** 维护（本文 §14 只放链接），避免两处漂移。
3. 改动共享文件（§9.3 清单）必须先说一声。
4. 接口要变 → 先提 PR 改本文和 `Launcher.Protocol`，再改实现。

---

## 1. 项目概述

**一句话**：一个启动快、装得稳的 **Windows** Minecraft Java 版启动器；内核与 UI 彻底分离，内核可被 CLI 与测试直接驱动。

### v1 范围（必须）

- 版本列表、原版安装（镜像 / 分片 / 断点 / SHA1 校验 / 修复）
- 启动游戏（参数合并、classpath、natives、日志、退出归因）
- **离线账户**（M3 必备，不依赖微软登录）
- Java 探测与自动选择、JRE 自动下载
- 微软 Device Code 登录、authlib-injector 外置登录
- 实例隔离、外部 `.minecraft` 导入
- 加载器：Fabric / Forge（三套机制）/ NeoForge
- 模组启停、整合包导入（Modrinth / HMCL / MultiMC；**CF 仅直链包**，见 §6.3.1）
- 自更新、诊断包导出、设置持久化、历史日志查看

### 非目标

- **v1 非目标：跨平台**。锁 Windows 的原因：DPAPI、注册表 Java 探测、MSI/签名、Windows 命令行限制处理都是平台相关；四人月内做双向适配必然两头不靠。
- v1 不做：AI 助手、P2P 联机、皮肤站托管、Bedrock、模组市场运营。
- v2 候选：Linux/macOS（先抽 `ICredentialProtector` / `IJavaRegistry` / `IPlatformPaths` 三个平台接口）、AI 工具、联机。

---

## 2. 技术栈与环境（v0.5 修订）

| 项 | 选型 | 修订说明 |
|---|---|---|
| 运行时 | **只钉一个 TFM**：优先 `net10.0-windows`；若两人环境不一致，统一降到 `net8.0-windows` | 「10 与 8 API 兼容」不成立；用 `global.json` 钉 SDK |
| UI | Avalonia 12.1.4 | Windows 优先；M0 实际使用版本 |
| MVVM | CommunityToolkit.Mvvm | |
| 日志 | `Microsoft.Extensions.Logging.Abstractions`（Core）+ **Serilog** 文件 sink（App） | Core 只依赖抽象；sink 选型统一 |
| CLI 解析 | **System.CommandLine** | 不手写解析，参数与门面语义统一 |
| HTTP 韧性 | **Polly.Core**（直接构造 `ResiliencePipeline`） | 不用 `Microsoft.Extensions.Http.Resilience`，它需要 `IHttpClientFactory`/DI，与 §3 冲突 |
| JSON | System.Text.Json + Source Generator | 禁 Newtonsoft |
| 数据库 | Microsoft.Data.Sqlite（WAL） | |
| 更新/分发 | **Velopack only**（`Setup.exe` + delta + `Portable.zip`） | 不再产出 MSI，避免两套安装器互相覆盖 |
| 架构测试 | NetArchTest.Rules | 守护 §3 的三条规则 |
| 复用库（可选） | **CmlLib.Core（MIT，M1 前优先评估）** | 只复用其元数据/下载/启动原语，接口仍由 `Launcher.Core` 定义；**不引入 PCL 源码** |
| 代码签名 | **不做**（无 Authenticode，也无应用层公钥签名） | 决策已定；未签名发布，靠 HTTPS + SHA-256 + SmartScreen 引导 |
| 完整性校验 | **SHA-256 清单**（自身 `integrity.json` + 更新清单 `sha256`） | v1 必做；安全边界见 §12.1 |

### 必须存在但容易漏的工程文件

```
global.json                      # sdk.version + rollForward: latestFeature
Directory.Build.props            # TFM、LangVersion、Nullable、TreatWarningsAsErrors
Directory.Packages.props         # 中央包版本（共享文件）
.editorconfig                    # dotnet format 依赖它，缺了 CI 必红
NuGet.config                     # 仓库内只保留 nuget.org；国内镜像放本机用户级配置
.config/dotnet-tools.json        # 钉死 dotnet-format 版本，防本机/CI 漂移
.gitattributes                   # * text=auto eol=lf；*.bat eol=crlf
.gitignore
```

### 环境检查（两人各跑一遍，结果贴群里）

```powershell
dotnet --list-sdks        # 必须与 global.json 一致
git config --global core.longpaths true
git config --global core.autocrlf false   # 与 .gitattributes 配合，避免双重转换
```

> **路径警告**：当前工作区 `D:\启动器` 含非 ASCII 字符，Velopack、部分 MSBuild target 和签名工具对它敏感。**构建/发布目录请迁到纯 ASCII 路径**（如 `D:\dev\lumen`），或至少把 `--artifacts-path` 指到 ASCII 目录。仓库路径本身也建议 ASCII。

---

## 3. 架构与依赖规则（v0.5 修订）

```
┌──────────────── Launcher.App (Avalonia, MVVM) ────────────────┐
│  Views / ViewModels / Services(Navigation, Dialog, TaskCenter)│
│            │ 只依赖 Launcher.Protocol + ILauncherFacade        │
├────────────▼──────────────────────────────────────────────────┤
│  Launcher.Core（无 UI、无容器、可被 CLI 调用）                 │
│  Meta │ Download │ Install │ Java │ Launch │ Auth │ Instances  │
│  Storage │ Diagnostics                                        │
└────────────┬──────────────────────────────────────────────────┘
             ▼  HttpClient / FileSystem / Process
      piston-meta / BMCLAPI / Yggdrasil / 磁盘 / java.exe
```

**不可违反的三条规则**（NetArchTest 守护，CI 会跑）：

1. `Launcher.Core` 不得引用 `Avalonia.*`、`Microsoft.Extensions.Hosting`、任何 DI 容器注册 API。
   - 允许：`Microsoft.Extensions.Logging.Abstractions`、`Polly.Core`、BCL。
   - Core 的构造由宿主手工 new / 简单工厂，不依赖 ServiceCollection。
2. `Launcher.App` 不得直接 new Core 内部实现，只能通过 DI 拿到 `ILauncherFacade`。
3. `Launcher.Protocol` **只放 DTO、枚举、接口定义**；不得有实现、IO、第三方依赖。

---

## 4. 解决方案结构与文件归属

```
Launcher.sln
Directory.Build.props / Directory.Packages.props / global.json
.editorconfig / NuGet.config / .config/dotnet-tools.json
.gitattributes / .gitignore
technoledge.md（共享） / README.md（共享，进度唯一载体）
LICENSE（MIT，共享） / THIRD_PARTY_NOTICES.md（共享）

src/
  Launcher.Protocol/             # 共享：仅 DTO/枚举/接口
    Versions/*.cs  Instances/*.cs  Progress/*.cs  Accounts/*.cs
    Mods/*.cs  Updates/*.cs  Logs/*.cs
    Loaders/*.cs  Settings/*.cs  Tasks/*.cs
  Launcher.Core/                 # 归属：A
    Meta/        VersionManifest.cs VersionDetail.cs RuleEvaluator.cs VersionResolver.cs
    Download/    DownloadItem.cs DownloadEngine.cs MirrorSelector.cs SpeedLimiter.cs
                 ResumeMetadata.cs DownloadQueue.cs
    Install/     IInstallStep.cs InstallJournal.cs VanillaInstaller.cs FabricInstaller.cs
                 ForgeInstaller.cs NeoForgeInstaller.cs ModpackInstaller.cs RepairService.cs
    Java/        JavaLocator.cs JavaRegistryScanner.cs JavaRuntime.cs JavaVersionPolicy.cs
                 JreDownloader.cs
    Launch/      ArgumentBuilder.cs LaunchService.cs ArgFileWriter.cs ProcessRunner.cs
                 ExitCodeAnalyzer.cs
    Auth/        OfflineAuth.cs MicrosoftAuth.cs AuthlibInjectorAuth.cs
                 ICredentialProtector.cs DpapiCredentialProtector.cs TokenStore.cs
    Instances/   InstanceManager.cs IsolationPolicy.cs ExternalScanner.cs NativesManager.cs
    Storage/     LauncherPaths.cs AppSettings.cs SqliteStore.cs Migrations/*.cs AtomicFile.cs
    Diagnostics/ CrashAnalyzer.cs DiagnosticBundle.cs LogRedactor.cs
  Launcher.App/                  # 归属：B
    App.axaml  Program.cs
    Views/  ViewModels/  Services/  Assets/  Mock/FakeLauncherFacade.cs
  Launcher.Cli/                  # 归属：A
    Commands/ install.cs launch.cs java.cs diag.cs

tests/
  Launcher.Core.Tests/           # 归属：A（唯一归属，B 不直接改；需要用例提 issue 或 PR 由 A 合）
  Launcher.App.Tests/            # 归属：B
  Launcher.Integration.Tests/
    Core/*.cs                    # 归属：A
    App/*.cs                     # 归属：B
```

### 共享文件（改动前必须在群里说，PR 需对方 review）

`Directory.Packages.props`、`Launcher.sln`、`Launcher.Protocol/**`、`README.md`、`technoledge.md`、`LICENSE`、`THIRD_PARTY_NOTICES.md`、`.github/**`、`build/**`。

> SDK 风格 csproj 自动包含 `.cs`，**新增文件不需要改 csproj**；只有新增 NuGet 包才动 `Directory.Packages.props`。

---

## 5. 接口契约（v0.4）

UI 只认下面的门面；Core 内部接口允许 A 自由重构，但不能破坏 `ILauncherFacade` 与 Protocol DTO。

```csharp
namespace Launcher.Protocol;

public interface ILauncherFacade
{
    // ---------- 快照 / 元数据 ----------
    Task<LauncherSnapshot> GetSnapshotAsync(CancellationToken ct);
    Task<VersionManifest> RefreshManifestAsync(ManifestRefreshMode mode, CancellationToken ct);
    Task<IReadOnlyList<LoaderVersionInfo>> GetLoaderVersionsAsync(
        LoaderKind loader, string gameVersion, CancellationToken ct);

    // ---------- 实例查询 / 任务查询 ----------
    Task<InstanceDetail> GetInstanceAsync(string instanceId, CancellationToken ct);
    Task<IReadOnlyList<TaskInfo>> GetTasksAsync(CancellationToken ct);
    Task CancelTaskAsync(string taskId, CancellationToken ct);
    Task RetryTaskAsync(string taskId, CancellationToken ct);

    // ---------- 安装 / 修复 / 加载器变更 ----------
    Task<InstanceSummary> InstallAsync(InstallRequest request,
        IProgress<InstallProgress> progress, CancellationToken ct);
    Task UpdateInstanceAsync(string instanceId, UpdateInstanceRequest request,
        IProgress<InstallProgress> progress, CancellationToken ct);
    Task RepairInstanceAsync(string instanceId,
        IProgress<InstallProgress> progress, CancellationToken ct);
    Task<InstanceSummary> InstallModpackAsync(ModpackInstallRequest request,
        IProgress<InstallProgress> progress, CancellationToken ct);

    // ---------- 启动 / 结束 ----------
    Task<GameSession> LaunchAsync(string instanceId,
        IProgress<LaunchEvent> events, CancellationToken ct);
    Task StopGameAsync(string instanceId, StopMode mode, CancellationToken ct);
    Task<IReadOnlyList<RunningSession>> GetRunningSessionsAsync(CancellationToken ct);

    // ---------- 账户 ----------
    Task<AccountSummary> LoginAsync(LoginRequest request,
        IProgress<LoginEvent> progress, CancellationToken ct);
    Task LogoutAsync(string accountId, CancellationToken ct);
    Task SetActiveAccountAsync(string accountId, CancellationToken ct);

    // ---------- 模组 ----------
    Task<IReadOnlyList<ModEntry>> ScanModsAsync(string instanceId, CancellationToken ct);
    Task SetModEnabledAsync(string instanceId, string modId, bool enabled, CancellationToken ct);

    // ---------- 实例运维 ----------
    Task DeleteInstanceAsync(string instanceId, bool keepSaves, CancellationToken ct);
    Task<IReadOnlyList<InstanceSummary>> ImportExternalAsync(string path,
        IProgress<ImportProgress> progress, CancellationToken ct);

    // ---------- 设置 ----------
    Task UpdateSettingsAsync(AppSettingsPatch patch, CancellationToken ct);

    // ---------- 更新 ----------
    Task<UpdateInfo?> CheckUpdateAsync(CancellationToken ct);
    Task ApplyUpdateAsync(UpdateInfo info, IProgress<UpdateProgress> progress, CancellationToken ct);

    // ---------- 日志 / 诊断 ----------
    Task<IReadOnlyList<LogFileInfo>> GetLogFilesAsync(string? instanceId, CancellationToken ct);
    Task<string> ExportDiagnosticsAsync(string? instanceId, CancellationToken ct);
}
```

### 关键 DTO 与语义

```csharp
// 「新建实例=安装」的修正：安装可复用已有实例（修复 / 重装 / 换加载器）
public sealed record InstallRequest(
    string? InstanceId,          // null = 新建；非 null = 安装到该实例
    string DisplayName,
    string VersionId,
    LoaderKind Loader,
    string? LoaderVersion,
    IsolationLevel Isolation,
    InstallMode Mode);           // New | Reinstall | ChangeLoader

public sealed record UpdateInstanceRequest(
    string? DisplayName, string? VersionId, LoaderKind? Loader,
    string? LoaderVersion, IsolationLevel? Isolation, int? MemoryMb);

public enum ManifestRefreshMode { IfStale, Force }
public enum InstallMode { New, Reinstall, ChangeLoader }
public enum StopMode { Graceful, Force }        // Graceful=请求关窗；Force=终止进程树
public enum AuthMode { Offline, Microsoft, Authlib }

// 登录请求必须显式带模式（M0 就要离线，不能靠参数猜）
public sealed record LoginRequest(
    AuthMode Mode,
    string? Username,            // Offline / Authlib
    string? ServerUrl,           // Authlib
    string? Password);           // 仅 Authlib；不落盘、不进日志

// 任务中心的数据来源：重启后从这里恢复
public sealed record TaskInfo(
    string TaskId, string? InstanceId, TaskKind Kind, TaskState State,
    double Percent, string Message, int Attempts, string? ErrorCode);

// Percent = -1 表示总量未知；UI 用按阶段加权的不确定进度条
public sealed record InstallProgress(
    InstallStage Stage, double Percent, double SpeedBytesPerSec,
    long BytesDownloaded, long TotalBytes,
    string? CurrentFile, string Message, string? ErrorCode = null);

public sealed record ImportProgress(
    string Stage, double Percent, long BytesDone, long TotalBytes, string? CurrentPath);

public sealed record LaunchEvent(LaunchEventKind Kind, string Line,
    int? ExitCode = null, string? ErrorCode = null, string? CrashHint = null);

public sealed record GameSession(string InstanceId, int ProcessId,
    Task<int> ExitTask);         // 进程独立存活，启动器退出不杀游戏

// ---------- 以下为 v0.5 补齐的 DTO：B 的 FakeLauncherFacade 依赖这些字段 ----------
public sealed record LauncherSnapshot(
    AppSettings Settings,
    IReadOnlyList<InstanceSummary> Instances,
    IReadOnlyList<AccountSummary> Accounts,
    string? ActiveAccountId,
    IReadOnlyList<JavaRuntime> Javas,
    DateTimeOffset? ManifestUpdatedAt,
    string AppVersion);

public sealed record InstanceSummary(
    string Id, string Name, string VersionId,
    LoaderKind Loader, string? LoaderVersion,
    IsolationLevel Isolation, InstanceState State,
    string? IconPath, DateTimeOffset? LastPlayedAt);

public sealed record InstanceDetail(
    InstanceSummary Summary, string GameDir, string? JavaPath,
    int MemoryMb, string ExtraJvmArgs, int ModCount, long DiskUsageBytes);

public sealed record AccountSummary(
    string Id, AuthMode Type, string Username, string Uuid,
    string? AvatarUrl, bool IsActive, DateTimeOffset? ExpiresAt);

public sealed record ModEntry(
    string Id, string Name, string? Version, string FileName,
    bool Enabled, LoaderKind Loader,
    IReadOnlyList<string> Dependencies, string? IconPath);

public sealed record RunningSession(
    string InstanceId, int ProcessId, DateTimeOffset StartedAt, string? VersionId);

public sealed record ModpackInstallRequest(
    string FilePath, string? InstanceName, IsolationLevel Isolation, ModpackKind Kind);

public sealed record AppSettingsPatch(
    int? MaxMemoryMb = null, string? GameDir = null, string? JavaPath = null,
    ProxyMode? ProxyMode = null, string? ProxyAddress = null,
    DownloadSourceMode? DownloadSource = null, int? MaxThreads = null,
    bool? AutoUpdate = null, string? AccentColor = null);

public sealed record LoaderVersionInfo(string Version, bool Stable, DateTimeOffset? ReleasedAt);
public sealed record JavaRuntime(string Path, int MajorVersion, bool Is64Bit, string Vendor, string? VersionString);
public sealed record UpdateInfo(string Version, string Notes, long Size, string Sha256, string Url);
public sealed record LogFileInfo(string Path, string? InstanceId, LogKind Kind, long Size, DateTimeOffset LastWriteAt);

public enum TaskKind { Install, Repair, Modpack, Launch, Update, Import }
public enum TaskState { Queued, Running, Paused, Succeeded, Failed, Cancelled }
public enum InstanceState { Installing, Ready, Broken, Repairing }
public enum ModpackKind { Modrinth, Hmcl, MultiMc, CurseForgeDirect }
public enum LogKind { Launcher, Game }
public enum ProxyMode { System, Manual, None }
public enum DownloadSourceMode { Auto, Official, Mirror }
```

**`LaunchAsync` 的 CancellationToken 语义（明确定义）**：只作用于启动前阶段（准备文件、拼装参数、创建进程）。进程 `Started` 之后取消**不会**杀游戏，调用方只能停止订阅事件；要结束游戏必须显式调用 `StopGameAsync`（`Graceful` 先 `CloseMainWindow` 等待 10s，超时或 `Force` 才终止进程树）。

**进度回调线程与节流（写死，避免 UI 跨线程崩）**：
- `IProgress<T>` 回调可能在任意线程（线程池 / 解压线程 / 下载线程）；VM **必须** `Dispatcher.UIThread.Post(...)` 后再改 `ObservableCollection`。
- Core 侧节流：同一任务最多 **10 次/秒**（≥100ms 合并），但**首帧与末帧必发**；asset 批量阶段用聚合进度，不逐文件回调。
- 回调只传不可变 record，不传可变对象。

### 契约变更流程

1. 提 PR 改 `Launcher.Protocol` + 本文 §5（表格登记版本与变更点）。
2. 对方 review 通过后合并。
3. A 更新实现，B 更新 Mock 与 VM，各自一个 PR。

| 契约版本 | 日期 | 变更 | 提出人 |
|---|---|---|---|
| v0.1 | 2026-10-24 | 初版门面与进度模型 | 共同 |
| v0.2 | 2026-10-24 | 门面补齐 9 类能力；`InstallRequest` 支持复用实例；`Percent=-1`；`RefreshManifestAsync` 改枚举；明确 CT 语义 | 评审反馈 |
| v0.3 | 2026-10-24 | 决策定稿：不签名仅 SHA-256；MIT；GitHub 私有；bangbang93 为发布门禁 | 决策 |
| v0.4 | 2026-10-24 | 补 `StopGameAsync/GetTasksAsync/GetInstanceAsync/SetActiveAccountAsync`；`LoginRequest` 显式 `AuthMode`；`ImportExternalAsync` 带进度；明确回调线程与节流 | 评审反馈 |
| v0.5 | 2026-10-24 | 补 `CancelTaskAsync/RetryTaskAsync/GetRunningSessionsAsync`；补齐全部 DTO 字段与枚举；明确 CmlLib 能力边界与 M0 门禁 | 复审反馈 |

---

## 6. 各模块实现约定（含必踩坑）

### 6.1 Meta（A）

- 继承合并：`parent` 先、`child` 覆盖；库去重键 `group:artifact[:classifier]`。
- `rules` 支持 `os.name / os.arch / os.version / features.*`。
- **变量断言范围**（修订）：只校验 `library.name / artifact.path / artifact.url` 中不允许残留 `${...}`；`arguments.game/jvm` 与 legacy `minecraftArguments` 中的 `${auth_player_name}` 等**必须放行**，由 §6.5 启动阶段统一校验。
- 缓存：manifest 15 分钟；version.json 按 SHA1 永久缓存。

### 6.1.1 CmlLib.Core 能力边界（M0 结束前必须定）

原则：**单点集成，禁止双路径并存**。采纳 CmlLib 的模块必须替换我们的实现，不允许"它的下载器 + 我们的下载器"同时存在，否则两套缓存、重试、日志，后期无人敢删。

| 能力 | 结论 | 理由 / 约束 |
|---|---|---|
| 下载调度（分片、镜像、`.part.meta`、单 host、磁盘预检） | **必须自研** | CmlLib 自带 Downloader 会绕开 §6.2 全部策略 |
| 安装 journal / 状态机 / 修复 | **必须自研** | 它不写我们的 journal，M2 验收「中断后可继续/修复」直接落空 |
| 参数拼装 / `@argfile` / Java 8 wrapper | **必须自研（最多只读参考）** | 必须实测其 `MLauncher` 是否把 `-cp` 拼成单串；若绕过 32K 方案则不可用 |
| Java 探测与版本约束 | **必须自研** | 需要注册表双视图、32/64 位规则、探测缓存 |
| 账户存储 / DPAPI / 刷新调度 | **必须自研** | 凭据策略与日志脱敏由我们负责 |
| 实例隔离 / 外部导入 | **必须自研** | 目录布局与 DB 权威由我们定义 |
| 版本 JSON 解析 / 继承合并 | **只读参考**，不引依赖 | 需按我们的 rules 与断言范围扩展；模型可借鉴，代码不复制 |
| NBT / 资源包 / 游戏设置解析、ZIP 安全解压、semver、Maven 路径 | **可复用** | 无状态工具；接入前登记组件表 |
| 微软 / 外置认证协议原语 | **可复用（仅协议）** | token 存储与刷新调度仍由我们做 |

**M0 门禁（写死）**：
1. M0 结束前必须给出评估结论（可复用 / 自研 / 不引入）；**未出结论默认全部自研**，M1 不得因此阻塞。
2. 若引入：验证依赖树**不含 Newtonsoft.Json**、不要求 DI 容器注册、`net10.0-windows` 可编译、且不会触发其 Downloader/installer 路径。
3. 结论写入 ADR：哪个模块用了它、边界在哪；CLI 与 GUI 必须走同一条实现。

### 6.2 Download（A）

**分片策略（修订）**

| 文件类型 | 策略 |
|---|---|
| asset（均值 ~100KB，单版本 3000+） | 单连接、串行小并发，**禁止分片**；批量写库 |
| library（多数 < 5MB） | 单连接 |
| client jar / JRE / 大库（> 16MB） | 8MB/片，单文件最多 8 片并发 |

**哈希策略（修订）**：SHA1 不能并行分段计算。流程固定为「分片并行写入预分配文件的不同偏移 → 全部完成后**顺序流式哈希** → 一致才 rename」。禁止边下边算分段哈希。

**镜像失败分类（修订，禁止"失败即降权"）**

| 失败类型 | 判定 | 处置 |
|---|---|---|
| DNS/连接超时/5xx | 网络 | 该 host 轻微降权；退避重试 |
| 429 / Retry-After | 限流 | **不降权**；按 `Retry-After` 或指数退避暂停该 host 60s |
| 404 | 该镜像无此文件 | 只标记 `(url,host)` 不可用，**不惩罚 host** |
| SHA1 不符 | 镜像内容错误/过期 | 该 host 冷却 30 分钟；文件隔离重下（优先官方源） |
| TLS/证书错误 | 配置 | 不重试，报 `E2005`，引导检查代理/系统时间 |

**续传元数据（新增）**：`.part.meta` JSON `{url,size,etag,lastModified,sha1,chunksBitmap}`；重启续传前校验 URL/size/ETag，不一致则丢弃重下。

**并发与退避参数（写死到配置默认值）**：全局 16、单 host 6、单文件 8 片；连接超时 10s、响应头 20s、读空闲 60s；重试 3 次，延迟 1s/2s/4s + 20% 抖动；429 优先 `Retry-After`；连续 429 自适应降低并发（最低 4）。

**下载源与优先级（新增，设置页“下载源”的数据来源）**

| 优先级 | 源 | 适用 |
|---|---|---|
| 1 | Mojang 官方（piston-meta / launchermeta / resources） | 元数据与原始文件；**SHA1 基准** |
| 2 | BMCLAPI（`bmclapi2.bangbang93.com`） | 国内镜像，按 Mojang 路径映射 |
| 3 | GitHub Releases / 自建镜像（由 `sources.json` 下发） | 更新包、备用镜像 |

- **源表外置**：`sources.json` 随包内置一份作为 fallback，同时可从 GitHub（HTTPS）拉取更新；换源不必发版。远程列表只允许 https，且每一项都要过下面的 URL 校验。
- **MCBBS 镜像已关站（2024）**，从默认表移除；仅当 `sources.json` 明确给出且实测可用时才启用。
- 设置页的“自动 / 官方 / 镜像”只切换首选集合，**不允许用户填任意 URL**（防投毒与 SSRF）。
- **单文件绑定单 host**：同一文件的所有分片必须来自同一 host；失败换源时整文件重下，不复用不同源的片。否则 SHA1 失败无法归因到具体镜像。

**URL 安全校验（P0，下载层统一执行，整合包清单里的 URL 也必须过）**：

1. 仅允许 `https`；拒绝 `http/file/ftp/data/javascript` 及带 userinfo 的 URL；端口只允许 443（或省略）。
2. host 白名单：`piston-meta.mojang.com`、`launchermeta.mojang.com`、`resources.download.minecraft.net`、`bmclapi2.bangbang93.com`、`api.modrinth.com`、`cdn.modrinth.com`、`maven.minecraftforge.net`、`maven.neoforged.net`、`github.com`、`objects.githubusercontent.com`、`raw.githubusercontent.com`，以及 `sources.json` 声明的镜像。
3. **DNS 解析后**校验 IP：拒绝私有网段（10/8、172.16/12、192.168/16）、环回（127/8、::1）、链路本地（169.254/16、fe80::/10）、`0.0.0.0`；防 DNS rebinding。
4. 重定向最多 3 跳，**每跳重新执行 1–3**；跨 host 重定向默认拒绝。
5. 整合包清单里的 `downloads[].url` 若无 `sha1/sha512` 且不在白名单 → 拒绝下载（或改为走我们支持的平台 API 解析）。
6. 代理模式下校验的是最终目标 URL 与解析结果，不是代理地址。

**其它**：
- `.part` + 原子 rename。
- **秒装快路径（替换“SHA1 一致直接跳过”）**：`size + mtime + blobs.verified_at` 命中且 30 天内 → 直接跳过，**不重算哈希**；仅 `RepairInstanceAsync` / 用户手动“检查完整性” / mtime 变化时全量重算。
- **持久化下载队列**（SQLite `download_queue`），启动时恢复未完成任务；队列行必须带 `instance_id / task_id / attempts / error`。
- **磁盘预检**：所需空间 = 总大小 × 1.2 + 512MB，不足直接 `E2004`。
- **代理**：设置项 `ProxyMode = System | Manual | None`，Manual 存地址/绕过列表；所有请求走同一 handler。
- 限速令牌桶（全局字节/秒，0 = 不限）。

### 6.3 Install（A）

统一 `IInstallStep`：`Prepare → Download → Extract → Patch → Finalize`，每步写 **install journal**（§7）。

| 加载器 | 机制 | 必需前置/坑 |
|---|---|---|
| Vanilla | client jar → libraries → natives → assets | ≤1.6 资源走 `virtual/legacy` |
| Fabric / Quilt | loader JSON 合并为子版本 | **v1 只验收 Fabric**；Quilt 保留实现位、不进 v1 验收 |
| Forge ≤1.12.2 | headless installer（`com.bangbang93.ForgeInstaller`） | 目标目录**必须先有 `launcher_profiles.json`**；已选定使用，许可证核验为发布门禁（§12） |
| **Forge 1.13–1.16.5** | **官方 installer `--installClient`**：读 `install_profile.json`，先确保 vanilla client jar 存在，再执行 `processors`（含 `--add-opens`） | 比 1.12 更复杂：`processors` 失败常见于缺失 vanilla jar/Java 版本不符 |
| Forge 1.17+ | bootstraplauncher + `-p` 模块路径 + `--add-opens/--add-exports` | 参数模板独立存放 |
| NeoForge | 1.20.2+ 与 Forge 分道 | 版本号规则不同 |
| 整合包 | CurseForge `manifest.json`；Modrinth `modrinth.index.json`；HMCL `modpack.json`；MultiMC `mmc-pack.json` + `instance.cfg` | 文件名写死，禁止各方猜；**只补清单声明的依赖**，`mods.toml` 运行时依赖无法推导，M7 验收不要求 |

### 6.3.1 CurseForge 决策（已定：方案 C）

CF 的 `manifest.json` 只给 `projectID/fileID`，取下载地址必须调 CF API，而 API 强制 apiKey 且不允许在公开客户端里硬编码分发。四条路互斥：

| 方案 | 做法 | 代价 |
|---|---|---|
| A | 客户端内置 key | **不可行**（key 泄露/封禁，违反 CF 条款） |
| B | 用户自填自己的 CF key（DPAPI 存储） | 体验差，但纯客户端可行 |
| C | 只支持 Modrinth + HMCL + MultiMC；CF 仅接受“已含直链的第三方整合包” | 功能缩水，但零后端、零 key |
| D | 自建服务端代理（key 在服务端） | 需要后端与运维，与“纯客户端”矛盾 |

**已拍板：方案 C**。v1 不支持需要 CF API 的整合包；CF 包只有在自带直链（第三方镜像/作者直链）时才能导入。M7 验收范围为 **Modrinth / HMCL / MultiMC**。未来若要支持完整 CF，再评估 B/D，v1 不预留隐藏入口。

**安装事务（新增，M2「装得稳」的支撑）**：
- 实例 `state ∈ {installing, ready, broken, repairing}`；
- 启动时发现 `installing` → 读 journal 继续或清理；
- `RepairInstanceAsync` 按 SHA1 重新校验并补缺；UI 提供"修复"入口。

### 6.4 Java（A）

- 探测顺序：实例指定 → 全局配置 → `JAVA_HOME` → **注册表（Registry32 与 Registry64 两个视图都读）** → 常见目录 → `PATH` → 内置 runtime → 提示下载。
  - 32 位视图必读：1.12.2 时代大量用户只装了 32 位 JRE。
- 解析优先 `release` 文件的 `JAVA_VERSION`，回退 `java -XshowSettings:properties -version`。
- 选择以 `version.json` 的 `javaVersion.majorVersion` 为准；兜底：≤1.16.5→8，1.17→16，1.18–1.20.4→17，≥1.20.5→21。
- **架构约束（澄清）**：**启动器本体只支持 x64 Windows**（ARM64 不验收）；**游戏 Java 可为 32 位，但仅允许 ≤1.12.2**。1.13+ 必须先过滤掉 32 位 JRE，否则会出现"选到 32 位 8u → 分配不到内存 → 启动崩溃"的难查问题。
- **JRE 下载（明确 raw/lzma）**：Mojang `java-runtime` manifest 的 `files` 同时提供 `raw` 与 `lzma`；**v1 只取 `raw`**（§2 只有 `System.IO.Compression`，不支持 LZMA）。若某组件只有 `lzma`，报 `E4003` 并引导换供应商 JRE，**不临时引入 7z 依赖**。下载后按 `sha1` 校验，落 `runtimes/`。

### 6.5 Launch（A）

- 变量表与三家一致；**未解析变量必须报 `E5002`**，不能把 `${...}` 原样传给游戏。
- 32K 命令行：Java 9+ 用 `@argfile`；Java 8 回退 wrapper jar（manifest `Class-Path`）。
- **argfile 编码（P0 修正，写死）**：
  - 编码由目标 JRE 主版本**静态决定**：**JDK ≤17 → 系统 ANSI 代码页**（`CultureInfo.CurrentCulture.TextInfo.ANSICodePage`，中文 Windows 为 936）；**JDK ≥18 → UTF-8**（JEP 400）。
  - 首次遇到某个 Java 路径时做一次 `sun.jnu.encoding` 探测，结果**缓存到 DB**（`java_path → encoding`）；之后启动不再起进程。
  - **`.NET Core` 的 `Encoding.Default` 恒为 UTF-8，禁止使用**；必须注册 `CodePagesEncodingProvider.Instance` 并 `Encoding.GetEncoding(ansiCodePage)`（.NET 10 已内置该类型，无需额外包）。
  - **禁止无条件 UTF-8**：含中文的实例/资源路径会变乱码，表现为 `Could not find or load main class`。
- **argfile 转义**：一行一个参数；参数含空格、制表、引号或 `#` 时用双引号包裹，并转义 `\` 与 `"`；不依赖 shell 引号规则。
- **argfile 生命周期（修订，P0-2）**：
  1. 仅当命令总长 > 24K 才写 argfile；
  2. 路径 `%TEMP%\lumen\<随机>\args.txt`，创建时用 `FileSecurity` 收紧 ACL 为当前用户；
  3. **等首行 stdout 或 `Started` 事件到达后再删**（`Process.Start` 只代表 CreateProcess 成功，JVM 可能还没解析 `@argfile`），最多等 5s；
  4. 删除遇 `ERROR_SHARING_VIOLATION` 重试 5 次（50ms 间隔），仍失败交给启动时清扫；
  5. 启动器每次启动清扫 1 小时前的残留；诊断包**显式排除** `args.txt`；日志中命令一律走 `LogRedactor`。
- `ProcessStartInfo.ArgumentList` 逐参传入；`UseShellExecute=false`；`CreateNoWindow=true`；stdout/stderr UTF-8。
- **默认内存与 JVM 参数策略（新增）**：
  - `memory_mb` 未设置：`min(物理内存 / 4, 8192)`，下限 2048；
  - ≥1.20.5 默认 G1（`-XX:+UseG1GC -XX:MaxGCPauseMillis=50`），不叠加与 Java 版本冲突的参数；
  - 用户覆盖值校验：≥512 且 ≤ 物理内存 × 0.8，否则 `E4002`。
- **退出码矩阵 + 归因**：0 正常；非 0 先读最新 `crash-reports/`；映射见 §8.2（`E5xxx`）。
- 游戏进程独立存活；显式结束走 `StopGameAsync`（§5）。

### 6.6 Auth（A）

- **平台抽象（新增）**：`ICredentialProtector`（`Protect/Unprotect`），v1 实现 `DpapiCredentialProtector`（CurrentUser 范围）。
  - **便携模式换机后果（明确写死）**：DPAPI 密钥与用户/机器绑定，拷贝到另一台机器后 `Unprotect` 必然失败 → 视为 token 失效，提示重新登录，**绝不回退明文**。
- 离线：`UUIDv3(md5("OfflinePlayer:"+name))`；token 生成合法 JWT 形状。
- 微软：Device Code → live token → XBL → XSTS → Minecraft；token 加密存储、自动刷新。
- 外置：authlib-injector **运行期下载并校验 SHA1**（不随包分发，见 §12），`-javaagent:...=地址`。
  - **SHA1 基准来源**：内置「版本 → SHA1」pin 表（随应用发布更新），只接受表内版本；表外版本一律拒绝（`E6003`）。禁止信任远端返回的 hash。
- **脱敏名单（修订）**：`--accessToken`、`clientId`、`refresh_token`、`client secret`、外置登录密码、session token、代理密码、`xuid`。

### 6.7 Instances（A）

- 隔离级别语义（修订，明确"隔离什么"）：

| 级别 | gameDir | 隔离内容 | 共享（只读） |
|---|---|---|---|
| `None` | `shared/game/`（标准 `.minecraft` 布局） | 无（老鸟模式，可能互相污染） | versions / libraries / assets / runtimes |
| `ModsConfig`（默认） | `instances/<id>/` | `mods/ config/ saves/ resourcepacks/ shaderpacks/ logs/ crash-reports/` | 同上 |
| `Full` | `instances/<id>/` | 上述 + `options.txt screenshots/` | 同上（**禁止复制 libraries/assets**，否则磁盘爆炸） |

- **natives 归属（修订）**：`shared/natives/<versionId>/<arch>-<hash>/`，按 **version + arch 唯一**，绝不实例内共享；解压时加文件锁，避免并行启动不同版本互相覆盖。
- 外部目录：PCL / HMCL / MultiMC / 原生 `.minecraft` 只读识别。导入二选一：
  - **复制导入（默认）**：把所选实例目录复制到 `instances/<id>/`，外部目录不动；适合"先试用"。
  - **原地接管**：`root_path` 直接指向外部目录，不复制；删除实例只解除登记，**永不删外部文件**。UI 必须明确提示"原地模式下本启动器会直接读写该目录"。
- **删除安全**：`DeleteInstanceAsync(keepSaves)`；`true` 时先把 `saves/`、`screenshots/` 移到 `exports/<instance>-<timestamp>/` 再删实例。

### 6.8 Diagnostics（A）

- 诊断包 = 日志（脱敏）+ 版本 JSON + Java 列表 + 系统信息 + 设置（脱敏）+ 实例配置。
- **排除项（修订）**：`accounts.json`、`args.txt`、`.part.meta`、任何 token 字段。
- 日志轮转：启动器日志单文件 20MB、保留 7 天、总量上限 200MB。
- **崩溃现场独立保留**：`crash-reports/` 不参与日志轮转，每实例保留最近 10 份；清理策略单独实现，避免把需要分析的现场一起删掉。

### 6.9 扩展点（v2 预留，v1 不实现）

`IAuthProvider`、`ILoaderInstaller`、`IPluginTool`（manifest 参考 VersePC：`tools[].risk`、`promptExtensions`）。

### 6.10 UI（B）

- 页面：`Home / Instances / Download / Accounts / Settings / Logs`。
- `TaskCenter` 统一任务队列：排队、取消、重试、**重启恢复**（状态来自 Core 的持久化队列）。
- VM 禁止文件 IO 与 `HttpClient`；只调 `ILauncherFacade` + `IProgress`。
- 内核未就绪用 `FakeLauncherFacade`（200ms 一次假进度，含 `Percent=-1` 的不确定态）。
- **性能预算**：冷启动到首页可交互 ≤ 1.5s（manifest 走缓存、刷新异步、**不做全量完整性哈希**，见 §12.1）；50 个实例列表 ≤ 200ms；UI 空闲内存 < 150MB。
- **稳定性**：单实例互斥锁（命名 Mutex）；全局异常兜底 → 写日志 + 诊断包入口。

---

## 7. 数据与文件布局（v0.5 修订）

**目录拆分（P0-1）**：`%APPDATA%`（Roaming）在 AD 域/漫游配置环境会整包同步，数 GB 的 versions/libraries/assets 绝不能放进去；只有小配置放 Roaming。

```
%APPDATA%\Lumen\                 # 只放"小、需跟随用户"的配置
  launcher.json                  # 全局设置（代理、下载源、内存默认值）

%LOCALAPPDATA%\Lumen\            # 所有数据（不漫游）
  accounts.json                  # token 为 DPAPI 密文
  launcher.db                    # SQLite（WAL）
  logs\ launcher-YYYYMMDD.log
  instances\<id>\instance.json   # DB 的只读导出（见下）
  shared\
    game\                        # None 隔离级别的共享 .minecraft
    versions\ libraries\ assets\ runtimes\ natives\
  cache\ .part 与 .part.meta
  exports\<instance>-<ts>\       # 删除实例时保留的存档
```

- **instance.json 与 DB 的权威关系（明确）**：**DB 是唯一权威**；`instance.json` 只作只读导出与排障快照。UI/CLI 一律写 DB；检测到 json 被手工修改时不自动合并，提示"导入/覆盖"二选一。
- **便携模式**：`--portable` → exe 同级 `data\`；启动时先做可写探测（Program Files 下不可写则回退 `%LOCALAPPDATA%` 并提示，不静默失败）。
- **备份**：WAL 下直接复制 `launcher.db` 会得到不一致快照；备份前必须 `PRAGMA wal_checkpoint(TRUNCATE)`，或用 `VACUUM INTO 'backup.db'`。

### 7.1 路径安全与垃圾回收

- **路径校验（所有生成路径统一走 `PathGuard`）**：
  - 实例名/内部 ID 只允许 `[A-Za-z0-9._-]`，长度 ≤ 64；拒绝 Windows 保留名（CON/PRN/AUX/NUL/COM1..9/LPT1..9）与结尾点/空格。
  - 拼接后必须 `GetFullPath` 且仍位于对应根目录内；拒绝 `..`、绝对路径注入、UNC 与设备路径（`\\?\`）。
  - 解压前校验每个 entry 的目标路径（Zip Slip）；拒绝符号链接/junction 逃逸。
  - 外部目录允许在任意盘，但必须显式确认，且只读写其 `.minecraft` 结构。
- **blobs 垃圾回收**：空闲时执行 mark-and-sweep——以 `url_cache`、当前实例、下载队列为根标记；未被引用且 `verified_at` 超过 30 天的 blob 删除。安装/修复运行期间禁止 GC；每轮写日志（删除数量/释放字节）。
- `download_queue` 中 `state=done/failed` 且超过 7 天的行一并清理。

### Schema v3（含迁移）

```sql
CREATE TABLE schema_meta(key TEXT PRIMARY KEY, value TEXT);   -- version=3

CREATE TABLE instances(
  id TEXT PRIMARY KEY,
  name TEXT NOT NULL,
  icon TEXT,
  root_path TEXT NOT NULL,
  version_id TEXT NOT NULL,
  loader TEXT NOT NULL,
  loader_version TEXT,
  isolation INTEGER NOT NULL,
  state TEXT NOT NULL,            -- installing|ready|broken|repairing
  java_path TEXT,
  account_id TEXT,
  memory_mb INTEGER,
  extra_jvm_args TEXT,
  created_at TEXT NOT NULL,
  last_played_at TEXT
);

-- 内容寻址：asset/库以 SHA1 为键，避免 URL 变化导致重复下载
CREATE TABLE blobs(
  sha1 TEXT PRIMARY KEY,
  size INTEGER NOT NULL,
  local_path TEXT NOT NULL,
  verified_at TEXT
);
CREATE TABLE url_cache(              -- URL → blob 的映射与条件请求
  url TEXT PRIMARY KEY,
  sha1 TEXT,
  etag TEXT,
  last_modified TEXT,
  checked_at TEXT
);

CREATE TABLE mirrors(host TEXT PRIMARY KEY, success INTEGER, failure INTEGER,
  latency_ms INTEGER, cooldown_until TEXT);

CREATE TABLE download_queue(
  id TEXT PRIMARY KEY, task_id TEXT, instance_id TEXT,
  url TEXT, dest TEXT, sha1 TEXT, size INTEGER,
  state TEXT, priority INTEGER, attempts INTEGER DEFAULT 0,
  error TEXT, created_at TEXT, updated_at TEXT);

CREATE TABLE install_journal(
  instance_id TEXT, step TEXT, status TEXT, detail TEXT, updated_at TEXT,
  PRIMARY KEY(instance_id, step));
```

- 迁移：`Migrations/M001_Init.cs`、`M002_QueueColumns.cs`…按 `schema_meta.version` 顺序执行，单事务；迁移前先 `VACUUM INTO` 生成备份（不要直接复制 WAL 库）。
- 写入：`PRAGMA journal_mode=WAL; synchronous=NORMAL;`；asset 落库每 200 条一事务。

---

## 8. 编码规范与错误码

### 8.1 规范

- `Nullable=enable`、`TreatWarningsAsErrors`、`LangVersion` 由 `Directory.Build.props` 统一。
- Core 内 `CancellationToken` 透传；`ConfigureAwait(false)`；禁用 `async void`、`.Result`、`.Wait()`。
- 日志用占位符；注释写"为什么"；变量名英文、注释中文允许。
- 提交前：`dotnet format && dotnet build -warnaserror && dotnet test`。

### 8.2 错误码表（码 + i18n key；UI 不写死中文）

| 码 | i18n key | 含义 | 典型处置 |
|---|---|---|---|
| `E1001` | `err.meta.fetch` | manifest 拉取失败 | 检查网络/代理，换镜像 |
| `E2001` | `err.download.network` | 下载网络错误 | 自动重试，失败后提示 |
| `E2002` | `err.download.rate_limited` | 被限流（429） | 自动退避，提示稍后 |
| `E2003` | `err.download.hash_mismatch` | SHA1 校验失败 | 自动换源重下 |
| `E2004` | `err.download.disk_full` | 磁盘空间不足 | 提示清理/换目录 |
| `E2005` | `err.download.tls` | TLS/证书错误 | 检查系统时间/代理 |
| `E3001` | `err.install.loader_failed` | 加载器安装器失败 | 展示 installer 日志尾部 |
| `E3002` | `err.install.missing_profiles` | 缺少 `launcher_profiles.json` | 自动创建后重试 |
| `E3003` | `err.install.interrupted` | 安装中断（有 journal） | 提供"继续/修复" |
| `E4001` | `err.java.not_found` | 未找到合适 Java | 引导下载 JRE |
| `E4002` | `err.java.memory_out_of_range` | 内存参数越界 | 回落到合法范围 |
| `E4003` | `err.java.runtime_lzma_only` | java-runtime 仅提供 lzma | 引导换供应商 JRE |
| `E5001` | `err.launch.java_version` | Java 版本不匹配 | 建议切换 Java |
| `E5002` | `err.launch.unresolved_placeholder` | 参数变量未解析 | 报告版本 JSON 异常 |
| `E5003` | `err.launch.command_too_long`（**内部**，不展示） | 命令行超长 | 自动走 argfile/wrapper |
| `E5004` | `err.launch.crashed` | 游戏异常退出 | 关联崩溃报告归因 |
| `E6001` | `err.auth.microsoft_failed` | 微软登录失败 | 重试/检查网络 |
| `E6002` | `err.auth.token_invalid` | token 失效（含换机解密失败） | 重新登录 |
| `E6003` | `err.auth.authlib_version_untrusted` | 外置组件版本不在 pin 表 | 拒绝下载 |
| `E7001` | `err.instance.broken` | 实例损坏 | 提供修复 |
| `E8001` | `err.update.hash_mismatch` | 更新清单/更新包哈希校验失败 | 拒绝更新，保留当前版本 |
| `E8002` | `err.integrity.self_check_failed` | 自身文件完整性校验失败 | 拒绝自我替换，引导重新安装 |

---

## 9. Git 与并行协作

### 9.0 仓库与权限（已确认）

- 托管：**GitHub 私有仓库** `limpidus-xiaoche/Lumen`（已推送首个提交）。
- **身份模型（临时例外，2026-10-24 起）**：`ttss4096` 的 GitHub 账号暂不可用，当前以**仓库级 deploy key（有写权限）**推送。这是临时状态，不是目标形态。
  - 现在能做：拉取、推分支。
  - 现在不能做：作为 PR 作者 / approve、使用 CODEOWNERS、在网页端操作 Issue/PR。
  - 工作流：改动推到 `feat/*` 分支 → 由 owner（`limpidus-xiaoche`）在网页端发起/合并 PR；**不直推 main**（hook 仍生效）。
  - 退出条件：账号恢复或新建账号 → owner 加 collaborator + 用户级 SSH key → 本 deploy key 降为**只读（CI 专用）** → 恢复正常 PR 流程。
  - 安全：私钥只留本机、不转给他人；一旦怀疑泄露，立即让 owner 删除该 deploy key。
- **Deploy key 的目标形态**：仅 CI 只读。当前写权限是临时例外，迁移完成即降权。
- **分支策略（务实版）**：Free 计划无法启用 rulesets / 强制 review，所以**不存在"已生效的 main 保护"**。执行方式：团队约定 + `.githooks/pre-push`（拒绝直接推 main）+ CI 检测直推并报警；force push 仅作约定禁止。
- **Hook 安装（必需步骤）**：仓库内提交 `.githooks/pre-push`；每人执行 `git config core.hooksPath .githooks`。hooks 不随 clone 自动生效，M0 验收必须检查。
- `CODEOWNERS` 仅作 review 路由（Free 计划不强制）。
- CI：GitHub Actions `windows-latest`，跑 format/build/test；PR 必须全绿才能合并。
- 仓库不存 secrets；发布凭据（若有）只放 GitHub Environments，并开启人工审批。

### 9.1 分支模型

- `main` 始终可构建、可启动。
- 分支：`feat/core-download`、`feat/ui-home`、`fix/argfile-quote`。
- 一个 PR 只做一件事，改动 ≤ 400 行。

### 9.2 提交信息

```
feat(core/download): 支持 Range 分片与镜像失败分类
fix(core/launch): argfile 首行输出后再删除（共享冲突重试）
docs: 契约 v0.5
```

### 9.3 冲突预防

1. **目录归属唯一**：A 只动 `Core/`、`Cli/`、`Core.Tests/`、`Integration.Tests/Core/`；B 只动 `App/`、`App.Tests/`、`Integration.Tests/App/`。
2. 共享文件改前打招呼（§4 清单）。
3. DTO 一个 record 一个文件，减少同文件冲突。
4. 每天开工 `git pull --rebase`；收工前推 WIP 分支（标题 `[WIP]`）。
5. 不提交 `_probe/`、`bin/`、`obj/`、日志、`.vs/`；`_probe/` 仅本地取证，**未来开源时源码树也不得包含任何 PCL 内容**。

### 9.4 PR 检查单

- [ ] `dotnet build` 无警告 + `dotnet test` 通过
- [ ] Core 逻辑有单测
- [ ] 无 UI→Core 内部类型引用
- [ ] 无明文 token / 密钥 / argfile 内容
- [ ] 与本文契约一致，不一致已同步本文

---

## 10. 任务拆解与里程碑（v0.5 修订）

> 无时间估算，只排顺序与验收。`[A]`=核心，`[B]`=应用，`[AB]`=共同。

### M0 骨架 + 离线身份（修订：离线认证前移）——✅ 2026-10-24 完成

- `[A]` 解决方案、`global.json`/`Directory.*`/`.editorconfig`/`NuGet.config`/`dotnet-tools.json`
- `[A]` `LauncherPaths`、`AppSettings`、日志、**SQLite v3 + 迁移**、原子写、单实例互斥、全局异常兜底
- `[A]` 离线账户（UUIDv3 + 伪 JWT）——约 20 行，不单独占里程碑
- `[A]` `LICENSE`、`THIRD_PARTY_NOTICES.md`、`docs/legal/` 许可存证
- `[B]` Avalonia 壳 + 导航 + 六个空页 + 主题基线 + `FakeLauncherFacade`
- `[AB]` 冻结 §5 契约 **v0.5**；提交 `.githooks/pre-push`，每人执行 `git config core.hooksPath .githooks`
- `[A]` 产出 **CmlLib.Core 评估结论**（§6.1.1 门禁；无结论则默认自研）
- **验收**：两人 build 绿；六页可切换；架构测试通过；离线身份可生成；hook 生效（直推 main 被本地拒绝）。

### M1 元数据 + Java

- `[A]` manifest、version.json、规则判定、继承合并、`JavaLocator`（含 32/64 注册表视图）
- `[B]` 下载页版本列表、设置页 Java 列表
- **验收**：列出全部版本与本机 JDK（含仅 32 位的旧 JRE）。

### M2 原版安装

- `[A]` 下载引擎（阈值分片/顺序哈希/失败分类/续传 meta/持久队列/磁盘预检/URL 安全校验）+ Vanilla 安装 + journal + 修复
- `[B]` 任务中心、进度 UI、**`CancelTaskAsync`/`RetryTaskAsync`**、启动时恢复任务
- **验收**：装 1.21.x SHA1 全通过；断网续传；重复安装秒完成；中断后可"继续/修复"。

### M3 启动原版（离线）

- `[A]` `ArgumentBuilder`、`ArgFileWriter`（生命周期与编码）、`ProcessRunner`、**`GetRunningSessionsAsync` + 孤儿进程识别**、退出归因
- `[B]` 实例卡片、启动按钮、实时日志、历史日志列表
- **验收**：离线实例进游戏；含空格/中文/引号路径可用；异常退出给出 `E5xxx` 归因。

### M4 账户（收窄）

- `[A]` 微软 Device Code + authlib-injector（运行期下载）+ DPAPI 抽象
- `[B]` 账户页、登录流程、换机后重新登录引导
- **验收**：微软登录显示正版名；重启免登录；换机解密失败走重新登录；日志无明文 token。

### M5 加载器

- `[A]` Fabric / Forge（1.12 / 1.13–1.16.5 / 1.17+ 三套）/ NeoForge、`GetLoaderVersionsAsync`、变更加载器
- `[B]` 安装向导（版本 + 加载器 + 加载器版本列表）
- **验收**：三种加载器各装一个实例并成功启动。

### M6 实例与模组

- `[A]` 隔离语义、natives 归属、外部导入、模组扫描/启停、删除保留存档
- `[B]` 实例管理页、模组列表、导入向导、删除确认（保留存档选项）
- **验收**：两实例 mods 互不影响；并行启动不同版本不冲突；误删可保留存档。

### M7 整合包

- `[A]` Modrinth / HMCL / MultiMC（CF 不验收，仅直链包可导入）；只补清单声明的依赖
- `[B]` 拖拽导入、依赖确认、结果页
- **验收**：Modrinth / HMCL / MultiMC 导入后直接可启动；**CF API 路线不验收**（仅直链包可导入）；缺失依赖只补清单声明项。

### M8 发布

- `[A]` Velopack（Setup.exe + delta + Portable.zip）、诊断包、便携模式、**更新清单与自身完整性哈希清单（§12.1）**
- `[B]` 更新入口、首次启动引导、白名单帮助页、日志轮转设置
- **验收**：旧版可升级；诊断包可复现问题；无 MSI 产物；篡改更新清单/更新包/本地文件必须被拒绝（`E8001/E8002`）。

---

## 11. 测试与 CI（v0.5 修订）

- **单元（A）**：rules 20+ 组合；继承合并（Fabric/Forge 真实 JSON）；参数拼装（1.12 字符串 vs 1.21 数组）；**argfile 转义（空格/中文/引号）**；UUIDv3；Zip Slip；原子写；SHA1 顺序校验；失败分类。
- **UI（B）**：VM 状态迁移、Mock 进度（含 `Percent=-1`）、任务取消。
- **集成（分目录归属）**：本地静态服务器（1 版本 + 5 库 + 10 asset + 1 大文件跨片）+ 断点续传 + 429 模拟。
- **平台/环境（A）**：代理三模式（System/Manual/None）；磁盘不足 `E2004`；便携模式在只读目录（Program Files）回退；换机 DPAPI 失败（mock `ICredentialProtector`）；单实例互斥。
- **序列化（A/B）**：`System.Text.Json` 源生成必须显式注册全部 Protocol 类型；用反射遍历测试，漏注册即失败（否则只在运行时炸）。
- **压力/节流**：3000 个 asset 的安装，进度回调 ≤10 次/秒且首末帧必发。
- **篡改/完整性（A）**：改 1 字节更新包 / 本地文件 → 必须 fail-closed 并给 `E8001/E8002`；清单 `size` 与包不符、`sha256` 不符、缺失哈希 → 全部拒绝；不做签名与公钥轮换用例。
- **真机矩阵（按合法性约束，不做笛卡尔积）**：
  - `Java 8 32-bit` × `Forge 1.12.2`（唯一合法 32 位组合）
  - `Java 8 64-bit` × `Vanilla 1.12.2 / Forge 1.12.2 / Forge 1.16.5`
  - `Java 16` × `1.17 Fabric / Forge`
  - `Java 17` × `Forge 1.16.5 / Forge 1.20.1 / Fabric 1.20.4`
  - `Java 21` × `Vanilla 1.21 / Fabric 1.21 / NeoForge 1.21`
  - **非法组合不测**：`Java 8` × `1.18+`、`32-bit` × `1.13+`
  - **退出码矩阵**按无符号比较（`0xC0000005` → `unchecked((int)0xC0000005)`，否则永不为真）。
- **安全/边界（A）**：URL 校验（file:/localhost/私网/跨 host 重定向）、`sources.json` 远端不可用回退内置、java-runtime 仅 lzma 时报 `E4003`、CodePages 编码、任务取消/重试、`GetRunningSessionsAsync` 重启后识别游戏进程。
- **CI**：`dotnet format --verify-no-changes` → `build -warnaserror` → `test` →（tag）`publish`。

---

## 12. 发布、版本与合规（v0.5 修订）

- 分发：**只用 Velopack**，产物 `Setup.exe`、`*-delta.nupkg`、`Portable.zip`；**不产 MSI**。
- 代码签名：**v1 不做**（Authenticode 与应用层签名都不做）；发布说明明示未签名，首次启动给 SmartScreen 引导。
- 许可证：本项目采用 **MIT**（根目录 `LICENSE`，版权行格式 `Copyright (c) 2026 <作者/组织>`，落 M0 前填写）；所有随包第三方组件必须与 MIT 兼容，不兼容者只能运行期下载/调用，并登记到 `THIRD_PARTY_NOTICES.md`。
- **发布形态（定稿）**：`self-contained=true`、`PublishSingleFile=false`、`PublishTrimmed=false`、`ReadyToRun=true`。
  - 理由：不要求用户装 .NET；Velopack 对**目录型产物**做 delta 最稳；单文件会破坏 delta 与首次启动解压；裁剪对 Avalonia 反射不友好，风险大于收益。
  - 1.5s 冷启动预算按 R2R + 目录形态评估；超出时先查 manifest/DB/完整性快校验，不要改发布形态。
- 版本号 `主.次.修订`；tag `v0.x.y`；CHANGELOG 由 PR 标题汇总。
- **Minecraft EULA 立场**：不分发 client jar / assets / 原版文件，全部从 Mojang 官方或镜像下载；离线模式仅限用户本地自用，不提供任何绕过正版验证的机制。
- **PCL2 代码复用边界（可核验）**：不 fork、不复制其代码/资源/帮助文本。依据原文：`https://github.com/Meloong-Git/PCL/blob/main/LICENCE`（抓取日期 2026-10-24），本地存档 `docs/legal/pcl-license-2026-10-24.txt`。重度使用（基于仓库修改，或参考大量内容重新实现启动/下载/Mod 管理等实质功能）要求：名称必须以 `Plain Craft Launcher (PCL)` 开头、关于页首位署名龙腾猫跃并附赞助链接、公开源码并沿用该指南、实现正版/赞助劝导、Windows 主色相必须为蓝色。以上与 MIT + `Lumen` + 自有 UI 不兼容，因此只借鉴不受版权保护的交互思想与公开协议。**本仓库源码树（含未来开源版本）不含任何 PCL 内容**；`_probe/` 仅本地取证，永不入库、也不作为交付物。
- **可复用库**：`CmlLib.Core`（MIT）可作为元数据/下载/启动原语的候选实现，接入方式由 `Launcher.Core` 的接口决定；引入前需复核其 LICENSE 与维护状态。
- **第三方组件（新增：能下载就不打包）**

| 组件 | 许可证（待复核） | 是否随包 | 用途 / 处理 |
|---|---|---|---|
| authlib-injector | AGPL-3.0 | **否** | 运行期下载 + SHA1；关于页附源码链接 |
| `com.bangbang93.ForgeInstaller` | **未登记（发布门禁）** | **待定** | Forge ≤1.12.2；随包前必须核实许可证并登记 `THIRD_PARTY_NOTICES.md` |
| Forge / NeoForge installer | LGPL-2.1（待复核） | **否** | 运行期从官方 maven 下载 |
| Avalonia 12 | MIT | 是 | UI |
| CommunityToolkit.Mvvm | MIT | 是 | MVVM |
| Polly.Core | BSD-3-Clause | 是 | 韧性 |
| Microsoft.Data.Sqlite / SQLitePCLRaw | MIT / Apache-2.0 | 是 | 数据库 |
| Velopack | MIT | 是 | 更新/安装器 |
| Serilog（含 `Sinks.File`） | Apache-2.0 | 是 | 文件日志 |
| System.CommandLine | MIT | 是 | CLI 解析 |
| NetArchTest.Rules | MIT（待复核） | 否 | 仅测试 |
| CmlLib.Core | MIT（已复核） | 待定（优先评估） | M1 前评估；若引入仅作元数据/下载/启动原语 |

> BCL 自带项（如 `Microsoft.Win32.Registry`）无需登记，但在 `THIRD_PARTY_NOTICES.md` 里要说明运行时依赖清单。

### 12.1 完整性与更新校验（v1 定稿：SHA-256，不签名）

决策：v1 **不做任何代码签名**，只用哈希校验。三个参考样本（PCL2 / HMCL / VersePC）都没有 Authenticode；其中 VersePC 的 `integrity.json` 就是纯哈希清单。本项目的取舍：先拿到可验证的完整性，暂不引入私钥管理、轮换与审批流程；升级为公钥签名时保留可选字段。

| 对象 | 校验时机 | 依据 | 失败处理 |
|---|---|---|---|
| 自身文件 `integrity.json` | 启动时快校验（`版本 + 文件 mtime` 命中即跳过）；更新后首次启动 / 手动“检查完整性”时全量 | 构建时生成的 SHA-256 清单（随包） | 报 `E8002`，拒绝自我替换；提示重新安装 |
| 更新清单 `releases.<channel>.json` | 检查更新时 | GitHub Releases（HTTPS） | 解析失败/字段缺失 → 不更新，报 `E8001` |
| 更新包（`Setup.exe` / delta / zip） | 下载后、应用前 | 清单内 `size + sha256`，流式校验 | 删除临时文件，重试 2 次后引导手动下载 |

规则：

1. 清单字段固定：`version / minSupported / url / sha256 / size / publishedAt`；解析器**忽略未知字段**，为 v2 增加 `signature/keyId` 留位。
2. 版本单调递增，默认拒绝降级；强制降级只允许显式开关。
3. 校验顺序固定：`size` → 流式 `sha256` → 通过后才允许执行；任何一步失败即 fail-closed，日志写 `E8001/E8002`。
4. 自身完整性清单**排除**用户配置、日志、`accounts.json`、下载缓存；只覆盖随包发布的二进制与资源。
5. 首次启动与更新页给"Windows 已保护你的电脑 → 更多信息 → 仍要运行"的图文引导；不在程序里自动改 Defender 排除项。
6. **校验时机（P0-4）**：冷启动只校验 `Launcher.exe` 与清单本身，并按「程序版本 + 文件 mtime」缓存结果；全量哈希只在更新后首次启动或用户手动触发时执行，避免与 1.5s 启动预算冲突。
7. **`integrity.json` 由 CI 生成（禁止手工）**：发布流水线在 `publish` 后对产物目录计算 SHA-256、生成清单并打进包；delta 包使用同一清单版本；workflow 必须校验「清单 ↔ 产物」一致，不一致直接失败。

**安全边界（必须如实写进发布说明）**：

- 能防：传输损坏、镜像/CDN 投毒、缓存污染、错误文件被误执行。
- **不能防**：GitHub 账户或发布流水线被攻破（攻击者可同时替换包与清单）、本机被入侵后同时篡改程序与清单。这是 v1 明确接受的残余风险。
- 这层是 defense-in-depth，**不防逆向**，不要当反作弊或 DRM。
- 未来公开发布若需更高信任，再启用可选签名扩展（ECDSA P-256 + 内置公钥），不改变现有哈希流程。

---

## 13. 风险与决策记录（ADR 极简版）

| # | 决策/风险 | 结论 | 备注 |
|---|---|---|---|
| 1 | UI 框架 | Avalonia（Windows 优先） | 体积可控；Electron 不可接受 |
| 2 | 跨平台 | **v1 不做**，v2 再抽平台接口 | 避免 DPAPI/注册表/签名的双份成本 |
| 3 | 韧性库 | Polly.Core 直接构造 | 不用 M.E.Http.Resilience，避免 DI 依赖 |
| 4 | 分发 | Velopack only | 与 MSI 并行会互相覆盖 |
| 5 | 代码签名 | **v1 不签名**（Authenticode 与应用层签名都不做） | 决策已定；靠 HTTPS + SHA-256；v2 可加签名 |
| 6 | 命令行超长 | Java 9+ argfile，Java 8 wrapper | argfile 等首行输出后删，需重试 |
| 7 | Forge 安装 | ≤1.12.2 用 bangbang93（许可证为门禁）；1.13+ 走官方 installer | 1.13–1.16.5 用 install_profile + processors |
| 8 | 第三方合规 | 运行期下载 + SHA1 + 声明 | authlib-injector 不打包 |
| 9 | 路径 | 构建目录必须 ASCII | 当前 `D:\启动器` 含非 ASCII |
| 10 | 命名 | 代号 `Lumen`，发布前商标检索 | 避开 VersePC 前缀 |
| 11 | 杀软误报 | 未签名 + 首次启动引导 + 安装器分发 | 不预留证书预算；靠口碑与帮助文档降低流失 |
| 12 | 完整性 | SHA-256 清单 + fail-closed；残余风险：发布渠道被攻破 | 安全边界见 §12.1 |
| 13 | 仓库 | GitHub 私有（Free 计划），SSH 公钥；保护降级为约定 + pre-push hook | 两人各持自己的公钥，仓库零密钥 |
| 14 | 许可证 | 本项目 **MIT**；第三方不兼容则只运行期下载 | 需 `LICENSE` 与 `THIRD_PARTY_NOTICES.md` |
| 15 | Forge ≤1.12.2 | 使用 `com.bangbang93.ForgeInstaller`；许可证核验为发布门禁 | 回退到 ≤1.12.2 官方 installer headless **未经验证**，需先做 spike；不可行则自研 `install_profile` 解析 |
| 16 | CurseForge | **已定 C**：Modrinth/HMCL/MultiMC；CF 仅直链包 | M7 不验收 CF API 路线 |
| 17 | GitHub 计划 | **Free**：分支保护不可用 → 约定 + pre-push hook + CI | CODEOWNERS 仅作 review 路由 |
| 18 | 是否 fork PCL2 | **不 fork，也不复制其代码/资源** | 许可证为《PCL 分发有限许可》+ 合理使用指南；重度使用要求「PCL 开头命名 + 关于页署名 + 公开源码并沿用指南 + 正版/赞助劝导 + Windows 主色相蓝色」，与 MIT/Lumen 直接冲突 |
| 19 | 避免重复造轮子 | M1 前评估 **CmlLib.Core（MIT）** 覆盖元数据/下载/启动原语；接口不变 | 不引入 PCL 代码；评估结论在 M1 前落文档 |
| 20 | 是否 fork XMCL | **否决**（保持路线 C） | XMCL 为 MIT、Electron/TypeScript，功能完整；但与 .NET/Avalonia 栈不兼容，切换需整体转 Node。仅作 UX 参考，不复制代码 |
| 21 | CmlLib 复用边界 | 只复用无状态工具；下载/安装/启动/Java/账户/实例自研；M0 出结论 | 未出结论默认自研；**禁止双路径并存** |
| 22 | 协作身份 | **临时：deploy key 写权限**（账号暂不可用）；目标：collaborators + 用户级 SSH，deploy key 仅 CI 只读 | 分支推送 + owner 合并；账号恢复后迁移 |
| 23 | 发布形态 | self-contained + R2R + 非单文件/非裁剪 | Velopack delta 对目录型产物最稳 |

---

## 14. 进度与同步

**唯一载体：[README.md](./README.md)**。本文不再维护日志副本。

每日提交者在 README 末尾追加：

```
[YYYY-MM-DD] A: 完成 xxx / 阻塞: xxx；B: 完成 xxx
```

同步会三问：昨天完成什么 / 今天做什么 / 有没有阻塞契约。

---

## 15. 上手 Checklist（v0.4 修正）

```powershell
git clone git@github.com:limpidus-xiaoche/Lumen.git ; cd Lumen   # SSH 公钥认证；路径必须 ASCII
dotnet restore
dotnet build -warnaserror
dotnet test
dotnet run --project src/Launcher.App

# CLI：实例化安装，账户绑定在实例上
dotnet run --project src/Launcher.Cli -- java list
dotnet run --project src/Launcher.Cli -- install --instance sandbox-vanilla --mc 1.21.1
dotnet run --project src/Launcher.Cli -- launch sandbox-vanilla
dotnet run --project src/Launcher.Cli -- launch sandbox-vanilla --offline-name Dev  # 临时离线账户
```

第一天完成：1–4 全绿；B 能用 `FakeLauncherFacade` 看到假进度与不确定进度条。

---

## 16. 术语表

| 术语 | 含义 |
|---|---|
| manifest | 版本清单 `version_manifest_v2.json` |
| version.json | 单版本定义（库、参数、资源索引、主类） |
| inheritsFrom | 版本继承，加载器版本常见 |
| rules | 库/参数的条件（os/arch/features） |
| natives | 本地动态库，需按 version+arch 解压到独立目录 |
| assetIndex | 资源索引，objects 哈希寻址 |
| argfile | Java 9+ 参数文件，规避命令行长度限制 |
| journal | 安装事务日志，用于中断恢复与修复 |
| 隔离级别 | 实例间是否共享 versions/libraries/assets/mods |

---

## 17. 评审响应表（v0.4）

| 评审项 | 处理位置 |
|---|---|
| P0-1 跨平台矛盾 / DPAPI 抽象 | §1（v1 锁 Windows）、§6.6（`ICredentialProtector` + 换机后果）、§2 |
| P0-2 门面缺能力 / InstallRequest 复用 | §5（补齐 9 类方法、`InstanceId`、`InstallMode`） |
| P0-3 argfile 明文 token | §6.5（生命周期/ACL/删除/清扫/排除）、§6.6（脱敏名单）、§6.8 |
| P0-4 Core 与 Resilience 冲突 | §2、§3（改 Polly.Core）、§13 |
| P0-5 分发与签名矛盾（v0.2） | §2、§12、§13（Velopack only；v0.3 起完全不签名，以 §12.1 为准） |
| P0-6 里程碑依赖倒置 | §10（离线认证并入 M0/M3，M4 收窄） |
| P0-7 下载策略 | §6.2（阈值分片、顺序哈希、失败分类、`.part.meta`、退避参数、持久队列、磁盘预检） |
| P0-8 许可证 | §12（authlib 运行期下载、组件表、EULA 立场） |
| P1-1 变量断言范围 | §6.1（只校验 library 字段） |
| P1-2 Protocol 规则 | §3 规则 3、§4 |
| P1-3 进度日志双载体 | §0、§14（README 唯一） |
| P1-4 .NET 10/8 兼容 | §2（global.json + 单一 TFM） |
| P1-5 Forge 1.13–1.16.5 缺失 | §6.3（install_profile + processors + vanilla jar 前置） |
| P1-6 测试矩阵 | §11（Java 16、argfile 转义、退出码矩阵） |
| P1-7 CLI 语义 | §15（实例绑定账户、`--offline-name`、目录对应） |
| P1-8 测试归属 | §4（唯一归属，Integration 分目录） |
| P2 工程细节 | §2（工程文件）、§6.2/6.3（可靠性）、§6.4（双注册表视图）、§6.7（隔离与 natives/删除）、§6.10（性能预算/互斥/异常）、§7（schema v2/WAL/迁移）、§8.2（错误码）、§12（组件表） |
| 补充：完整性（v0.3 定稿） | §2、§8.2（E8001/E8002）、§10（M8）、§11（篡改用例）、§12.1（方案与安全边界）、§13-5/12 | 由"公钥签名"调整为"SHA-256 哈希校验" |
| 决策定稿 v0.3 | 头部、§9.0、§12、§13-13/14/15 | 代号 Lumen；A/B 保持；GitHub 私有 + SSH；MIT；bangbang93 以许可证为门禁 |
| v0.4 P0-1 数据目录漫游 | §7（APPDATA/LOCALAPPDATA 拆分）、§6.6 |
| v0.4 P0-2 argfile 竞态 | §6.5（等首行 stdout/Started、共享冲突重试、清扫兜底） |
| v0.4 P0-3 argfile 编码 | §6.5（`sun.jnu.encoding` 探测 + 版本兜底）、§11 |
| v0.4 P0-4 全量哈希 vs 冷启动 | §12.1（时机 + mtime 缓存）、§6.10 |
| v0.4 P0-5 CurseForge | §6.3.1（**已定 C**）、§10 M7、§13-16 |
| v0.4 决策：CF=C / GitHub=Free | §6.3.1、§9.0、§10 M7、§13-13/16/17 |
| v0.4 P1 契约补口 / 线程 / 节流 | §5（StopGame/GetTasks/GetInstance/SetActiveAccount/AuthMode/Import 进度 + 回调线程与 10Hz 节流） |
| v0.4 P1 下载源 / 快路径 | §6.2（源优先级、单文件单 host、size+mtime+verified_at）、§7 |
| v0.4 P1 Java 64 位 / 内存策略 | §6.4、§6.5、§8.2（E4002）、§11 |
| v0.4 P1 一致性 | §6.3（Quilt/包文件名/依赖口径）、§4（Protocol 目录）、§2（NuGet/Serilog/CLI）、§7（WAL 备份/队列字段/权威）、§9.0（GitHub 计划）、§13-15 |
| v0.4 P2 工程细节 | §6.7（None 目录）、§6.8（crash 保留）、§7（便携回退）、§8.2（i18n/E5003 内部）、§11（新增用例）、§12（组件表） |
| v0.4 版本记账 | 头部、§2/§3/§5/§7/§10/§11/§15/§17 标题、§5 契约表 |
| 补充：PCL2 许可核实 | §2（复用库）、§12（PCL2 边界 + 组件表）、§13-18/19 | 结论：不 fork；评估 CmlLib.Core（MIT） |
| 补充：XMCL 评估 | §2（复用库）、§12（组件表）、§13-20 | 结论：MIT 可 fork，但决定不切换；维持 .NET + CmlLib.Core |
| v0.5 P0-1 CmlLib 边界 | §6.1.1（能力表 + M0 门禁）、§2、§12、§13-21 |
| v0.5 P0-2 协作身份 | §9.0（collaborators / deploy key / 约定）、§10 M0、§13-22 |
| v0.5 P0-3 许可存证 | §12（原文链接 + 抓取日期 + 本地存档 + 源码树不含 PCL）、`docs/legal/` |
| v0.5 P0-4 编码兜底 | §6.5（CodePages + 静态决定 + 缓存）、§8.2、§12 组件表 |
| v0.5 P0-5 源表 / MCBBS | §6.2（移除 MCBBS、`sources.json` 外置） |
| v0.5 P0-6 任务取消/重试 | §5（`CancelTaskAsync/RetryTaskAsync`）、§10 M2 |
| v0.5 P0-7 下载 URL 安全 | §6.2（https/白名单/私网/重定向/无 hash 拒绝）、§11 |
| v0.5 P0-8 lzma | §6.4（只取 raw / `E4003`）、§8.2 |
| v0.5 P1 其余 | §5 DTO 字段、§5 `GetRunningSessionsAsync`、§9.0 hook 安装、§6.6 authlib pin、§12 发布形态、§12.1 CI 生成、§6.7 导入模式、§7.1 路径/GC、§10 M0 版本、§1 范围、§6.4 x64、§11 矩阵 |
