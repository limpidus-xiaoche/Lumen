# Checklist

- [x] 所有 `*.md` 的代码围栏计数为偶数，README 末段不再落入代码块
- [x] `docs/history/README-archive-2026-10.md` 存在，历史调研与复审原文内容完整、未被改写
- [x] README 首屏可在 3 屏内看到「项目简介 / 当前状态 / 待办」
- [x] README 当前区段无 `bangbang93.ForgeInstaller`、无 `` `SystemDecorations=Full` ``、无「B 负责」分工表述
- [x] technoledge 头部为唯一版本号，§17 标题不含版本号
- [x] technoledge §13 中 Forge ≤1.12.2 只有一条有效结论（官方 installer）
- [x] technoledge §4 目录树列出的文件均真实存在，且不再出现 `Launcher.sln`
- [x] technoledge §15 含 `AVALONIA_TELEMETRY_OPTOUT=1`
- [x] ui.md §9.1 首帧实测标注为未达标，§10 目录与源码一致，§16 无已完成待办残留
- [x] THIRD\_PARTY\_NOTICES 覆盖 Velopack、System.CommandLine、Microsoft.Extensions.Logging.Abstractions、StbImageSharp、Avalonia.Fonts.Inter
- [x] `scripts/docs-lint.ps1` 在当前仓库返回 0，且人为引入旧口径时返回非零
- [x] `.github/workflows/ci.yml` 在 PR 上执行 format/build/test/docs-lint 四项
- [x] `release.yml` 设置 `AVALONIA_TELEMETRY_OPTOUT=1`
- [x] `dotnet format whitespace --verify-no-changes` 通过，且未产生语义改动
- [x] `tests/Launcher.Render.Tests` 已加入 `Launcher.slnx` 并通过
- [x] PortalLayout 用例覆盖 0/1/3/7/8 实例与透明度 clamp
- [x] 架构测试覆盖「Render 不依赖 Avalonia」与「Core/Protocol 不被 Render 反向依赖」
- [x] 启动失败窗口不再出现 `lumen diagnostics`，指引路径真实可执行
- [x] `.gitattributes` 覆盖 `*.slnx`
- [x] 仓库根目录无 `forge-installer.jar.log`；`_probe/`、`build/` 未被删除
- [x] 全量验证：build 0 警告 0 错误、测试全通过、docs-lint 通过

