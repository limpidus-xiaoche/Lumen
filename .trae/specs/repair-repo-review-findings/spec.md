# 仓库审议问题全面修复 Spec

## Why

2026-10-25 的工作区与仓库文件审议发现：代码基线健康（构建 0 警告 0 错误、114 项测试全通过），但**声明与事实脱节**——README 的 Markdown 围栏损坏、Forge 决策在文档内自相矛盾、文档声明的 CI 门禁实际不存在、目录树文档与源码大面积不符、S1 渲染层无自动化测试。这些问题会让后续维护者按错误前提决策，需要在进入 S2 之前一次性收敛。

## What Changes

- 修复 `README.md` 的代码围栏不配对、旧决策残留（bangbang93、`SystemDecorations`），并拆分「当前状态 / 待办 / 近期日志」与历史归档。
- 统一 `technoledge.md` 的版本记账、Forge 决策口径、A/B 分工表述与 §4 目录树。
- 同步 `ui.md` 的性能预算状态、目录结构与过期待办。
- 补全 `THIRD_PARTY_NOTICES.md` 缺失的随包组件。
- 新增 PR 持续集成工作流与文档一致性脚本，把「文档围栏 / 旧口径残留 / 第三方登记」变成机器可查门禁。
- 新增 `Launcher.Render.Tests` 项目，覆盖 S1 纯函数与渲染边界；扩展架构测试到依赖方向不变量。
- 修复启动失败窗口指向不存在命令的问题。
- 记录 Avalonia 遥测 opt-out 的构建前置条件；补齐 `.gitattributes` 的 `*.slnx` 规则；清理工作区残留产物。

**不做**（明确非目标）：不合并分支、不推送 main、不打 tag、不实现 S2 交互、不引入新渲染功能、不改动 `Launcher.Core` 业务逻辑与 `ILauncherFacade` 契约。

## Impact

- 受影响规格：文档生命周期、测试与质量门禁、渲染层边界、发布构建可复现性。
- 受影响代码：
  - 文档：`README.md`、`technoledge.md`、`ui.md`、`THIRD_PARTY_NOTICES.md`
  - CI / 脚本：`.github/workflows/ci.yml`、`.github/workflows/release.yml`、`scripts/docs-lint.ps1`
  - 代码：`src/Launcher.App/App.axaml.cs`、`tests/Launcher.Render.Tests/**`、`tests/Launcher.Core.Tests/ArchitectureTests.cs`、`Launcher.slnx`
  - 工程配置：`.gitattributes`、`Directory.Build.props`（仅在确有必要时）

## ADDED Requirements

### Requirement: 文档一致性门禁

系统 SHALL 提供 `scripts/docs-lint.ps1`，对仓库文档做机器可查的一致性检查，并以非零退出码报告失败。

#### Scenario: Markdown 围栏损坏

- **WHEN** 任一 `*.md` 文件中 ``` 围栏计数为奇数
- **THEN** 脚本输出具体文件与计数并失败

#### Scenario: 当前口径残留旧决策

- **WHEN** 文档当前决策区段出现 `bangbang93.ForgeInstaller`（历史归档区除外）、`Launcher.sln`（应为 `Launcher.slnx`）、`` `SystemDecorations=` ``（应为 `WindowDecorations`）
- **THEN** 脚本列出命中行并失败

#### Scenario: 第三方组件未登记

- **WHEN** `Directory.Packages.props` 或任一 `*.csproj` 出现 `PackageReference`，但 `THIRD_PARTY_NOTICES.md` 无对应条目
- **THEN** 脚本输出差集并失败

### Requirement: PR 持续集成

系统 SHALL 提供 `.github/workflows/ci.yml`，在 `pull_request` 与推送到 `feat/**` 时执行：设置 `AVALONIA_TELEMETRY_OPTOUT=1` → `dotnet format whitespace --verify-no-changes` → `dotnet build -warnaserror` → `dotnet test` → `scripts/docs-lint.ps1`。

既有 switch 缩进风格 SHALL 通过 `.editorconfig` 固化，而不是对全仓库做无意义重排。

#### Scenario: 提交破坏构建

- **WHEN** PR 中的代码产生警告或编译错误
- **THEN** CI 失败并给出具体 job 与步骤

#### Scenario: 文档口径回退

- **WHEN** PR 中重新引入旧 Forge 决策或未闭合围栏
- **THEN** docs-lint job 失败

### Requirement: 渲染层测试与依赖边界

系统 SHALL 新增 `tests/Launcher.Render.Tests` 项目并纳入 `Launcher.slnx`，覆盖 `PortalLayout` 的布局、透明度 clamp 与折叠边界，`Camera` 的默认值与投影参数，以及 `SceneRenderer` 的纯函数状态判定。

系统 SHALL 在架构测试中断言：`Launcher.Render` 不依赖 Avalonia；`Launcher.Core` 与 `Launcher.Protocol` 不被 `Launcher.Render` 反向依赖。

#### Scenario: 门布局回归

- **WHEN** 实例数为 0 / 1 / 3 / 7 / 8
- **THEN** 门位次、步进与透明度符合 `ui.md` §8.1 与 clamp 规则

#### Scenario: 渲染层误引 UI 框架

- **WHEN** 有人在 `Launcher.Render` 引入 Avalonia 引用
- **THEN** 架构测试失败

### Requirement: 启动失败可执行指引

系统 SHALL 在 Core 初始化失败窗口中只给出用户实际可执行的路径指引（日志目录），不得指向未随包分发的 `lumen diagnostics` 命令。

#### Scenario: Core 初始化异常

- **WHEN** `CoreServices.Create()` 抛异常且未使用 `--fake`
- **THEN** 窗口展示错误信息与真实存在的日志目录路径，不出现不可执行命令

## MODIFIED Requirements

### Requirement: README 作为唯一进度载体

README SHALL 保持仓库根目录与「单一进度载体」地位，但必须满足：

- 所有代码围栏成对闭合；
- 首屏为「项目是什么 + 当前状态 + 待办」，历史调研与历次复审原文移入 `docs/history/README-archive-2026-10.md` 并以链接回引；
- 里程碑状态表覆盖 M0–M8 与 S1，且不再出现「B 负责」等已取消的分工表述；
- 旧决策只允许出现在历史归档区，当前区段不残留旧口径。

### Requirement: technoledge 作为技术事实源

`technoledge.md` SHALL 满足：

- 头部 `文档版本`、日期与开发者描述为唯一版本记账处，§17 标题不带版本号；
- §13 ADR 表内 Forge ≤1.12.2 只有一条有效结论：统一走官方 installer，不再引入 `com.bangbang93.ForgeInstaller`；
- §4 目录树与 `git ls-files src` 的实际文件一致，构建文件写作 `Launcher.slnx`；
- 头部与正文不再声称「两名并行开发者 A/B」为当前分工，保留职责边界说明即可。

### Requirement: ui.md 与实现同步

`ui.md` SHALL 在 §9.1 预算表中增加「当前实测 / 是否达标」列；目录结构改为实际文件；§16 中已完成的待办标记完成；§14 未决项与 S4/S6 的冲突给出明确结论（4:3 安全区按 S4 要求保留，S6 竖版另行裁剪）。

### Requirement: 第三方声明完整性

`THIRD_PARTY_NOTICES.md` SHALL 登记所有随包与构建依赖，至少补齐：Velopack（MIT）、System.CommandLine（MIT）、Microsoft.Extensions.Logging.Abstractions（MIT）、StbImageSharp（Public Domain）、Avalonia.Fonts.Inter（Inter 字体，OFL-1.1）。

### Requirement: 构建前置条件文档化

`technoledge.md` §15 与 `.github/workflows/release.yml` SHALL 显式设置 `AVALONIA_TELEMETRY_OPTOUT=1`，避免受限环境或 CI 因遥测目录不可写而构建失败。

## REMOVED Requirements

### Requirement: Forge ≤1.12.2 使用 bangbang93.ForgeInstaller

**Reason**: 官方 installer 已覆盖全部 Forge 版本且 1.20.1 真机验证通过；继续保留双口径会让发布合规门禁失义。

**Migration**: 文档与 README 速查统一改为「官方 installer `--installClient`」；历史日志区保留旧记录但标注「已撤销」，`docs-lint` 将当前区段的 `bangbang93.ForgeInstaller` 视为失败。

### Requirement: A/B 两人并行分工

**Reason**: `ui.md` §11 已取消目录归属限制，仅保留「Core 与契约不被 UI 反向改写」一条硬约束。

**Migration**: 删除当前区段的 A/B 分工声明；保留模块职责边界描述；历史记录中的分工条目保留但标注失效。
