# Tasks

- [x] Task 1: 修复 README 结构与旧口径
  - [x] SubTask 1.1: 修复代码围栏（第 327/628/642 行），确保任一 `*.md` 围栏成对
  - [x] SubTask 1.2: 新建 `docs/history/README-archive-2026-10.md`，把第三方调研结论与历次复审原文**逐字**迁入，README 保留链接
  - [x] SubTask 1.3: README 首屏重排为「项目简介 → 当前状态（M0–M8 + S1）→ 待办 → 最近日志」
  - [x] SubTask 1.4: 清除当前区段旧口径：`bangbang93.ForgeInstaller`、`SystemDecorations=Full`、已取消的「B 负责」分工表述

- [x] Task 2: 统一 technoledge 技术事实源
  - [x] SubTask 2.1: 头部保留唯一版本号，更新日期；开发者描述改为「单维护者 + 职责边界」，删除当前 A/B 分工声明
  - [x] SubTask 2.2: §17 标题去掉版本号，避免与头部冲突
  - [x] SubTask 2.3: §13 ADR 表内 Forge ≤1.12.2 仅保留「官方 installer」一条有效结论，旧行删除或标注已撤销
  - [x] SubTask 2.4: §4 目录树按 `git ls-files src` 实际文件重写；`Launcher.sln` 全量改为 `Launcher.slnx`
  - [x] SubTask 2.5: §15 上手命令前显式设置 `AVALONIA_TELEMETRY_OPTOUT=1`

- [x] Task 3: 同步 ui.md
  - [x] SubTask 3.1: §9.1 预算表新增「当前实测 / 是否达标」列（首帧 5.2–8.1s 标注未达标）
  - [x] SubTask 3.2: §10 目录树改为实际文件（`Gl.cs`、`SceneRenderer.cs`、`PortalLayout.cs` 等）
  - [x] SubTask 3.3: §16 已完成待办标记完成并清理失效行
  - [x] SubTask 3.4: §14 未决项给出结论（4:3 安全区按 S4 保留，S6 竖版另裁剪）

- [x] Task 4: 补全第三方声明
  - [x] SubTask 4.1: `THIRD_PARTY_NOTICES.md` 增加 Velopack、System.CommandLine、Microsoft.Extensions.Logging.Abstractions、StbImageSharp、Avalonia.Fonts.Inter（OFL-1.1）

- [x] Task 5: 文档一致性脚本与 CI 门禁
  - [x] SubTask 5.1: 新增 `scripts/docs-lint.ps1`：围栏奇偶、当前区段旧口径 token、`PackageReference` ↔ 第三方声明差集，失败返回非零
  - [x] SubTask 5.2: 新增 `.github/workflows/ci.yml`：`pull_request` + `push feat/**`，执行 telemetry opt-out、`dotnet format whitespace --verify-no-changes`、`dotnet build -warnaserror`、`dotnet test`、`docs-lint`
  - [x] SubTask 5.3: `release.yml` 增加 `AVALONIA_TELEMETRY_OPTOUT=1`
  - [x] SubTask 5.4: 让现有代码通过格式检查：优先在 `.editorconfig` 固化既有 switch 缩进风格，避免无意义的大面积重排

- [x] Task 6: 渲染层测试与架构边界
  - [x] SubTask 6.1: 新建 `tests/Launcher.Render.Tests`（xunit + NetArchTest + 引用 `Launcher.Render`），加入 `Launcher.slnx`
  - [x] SubTask 6.2: `PortalLayout` 用例：0/1/3/7/8 实例、左右交替位次、opacity clamp
  - [x] SubTask 6.3: `Camera` 用例：默认位置/目标/FOV/near/far 与投影矩阵可复现
  - [x] SubTask 6.4: 扩展架构测试：`Launcher.Render` 不依赖 Avalonia；`Launcher.Core`/`Launcher.Protocol` 不被 Render 反向依赖

- [x] Task 7: 修复启动失败窗口指引
  - [x] SubTask 7.1: `App.axaml.cs` 错误窗口只展示真实存在的日志目录路径，移除 `lumen diagnostics` 命令指引

- [x] Task 8: 工作区卫生
  - [x] SubTask 8.1: `.gitattributes` 增加 `*.slnx text eol=crlf`
  - [x] SubTask 8.2: 将根目录 `forge-installer.jar.log` 移出仓库目录（保留 `_probe/` 与 `build/`，不删除用户取证与构建产物）

- [x] Task 9: 全量验证
  - [x] SubTask 9.1: `dotnet format whitespace --verify-no-changes`、`dotnet build -warnaserror`、`dotnet test`、`scripts/docs-lint.ps1` 全部通过
  - [x] SubTask 9.2: 人工抽查 README 首屏、technoledge §4/§13、ui.md §9.1 的最终渲染与口径

# Task Dependencies
- Task 5 的 `docs-lint` 通过性依赖 Task 1–4 完成
- Task 5.4 依赖 Task 1–4 完成后执行，避免格式与文档改动交叉
- Task 6、Task 7 与文档任务互不依赖，可并行
- Task 9 依赖 Task 1–8 全部完成
