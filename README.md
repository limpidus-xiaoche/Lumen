# Lumen 启动器

Lumen 是一个 Minecraft 启动器：`.NET 10`（`net10.0-windows`）+ Avalonia 12 + `CmlLib.Core` 元数据能力，单进程 GUI + 可复用 `Launcher.Core` + `Launcher.Cli`。
v1 仅支持 **Windows 10/11 x64**，许可证 **MIT**；不签名，只用 SHA-256 清单做完整性校验（fail-closed），分发只走 Velopack（`Setup.exe` + Portable zip）。
当前状态：内核里程碑 M0–M8 全部完成，UI 阶段 **S1（世界层骨架）已完成，B 的 M1–M4 页面已合入，待开工 S2**。

## 当前状态

| 里程碑 | 状态 | 范围 |
|---|---|---|
| M0 | ✅ | 解决方案骨架（Protocol / Core / App / CLI）、SQLite v3、离线身份、六页 Avalonia 壳、8 项测试、pre-push hook |
| M1 | ✅ | 版本元数据与解析：manifest（含 `latest`）、version.json、rules、`inheritsFrom` 合并、Java 探测（32/64 注册表） |
| M2 | ✅ | 下载引擎（分片/Range 续传/SHA1/URL 安全）+ Vanilla 安装、install journal、秒装、修复、CLI install/repair |
| M3 | ✅ | 参数拼装与变量展开、Java 选择、argfile 编码/生命周期、进程启动与日志、退出归因、运行会话、CLI launch/stop/sessions |
| M4 | ✅ | 账户：DPAPI 整文件加密存储、微软 Device Code 登录（真机通过）、authlib-injector 外置登录、启动凭据注入 |
| M5 | ✅ | 加载器：Fabric / Forge / NeoForge 版本清单、Fabric profile 合并安装、Forge/NeoForge 官方 installer headless 安装、换加载器（NeoForge 代码就绪，本机 DNS 被污染未真机验收） |
| M6 | ✅ | 实例：`root_path` 为 gameDir 唯一权威、MultiMC/HMCL/原生实例复制导入、模组扫描与启停、删除保留存档 |
| M7 | ✅ | 整合包导入：Modrinth / HMCL / MultiMC / CF 直链（仅 overrides）、依赖下载 + SHA1 校验、overrides 复制 |
| M8 | ✅ | 完整性校验（快/全量，E8002）、更新清单 + 流式 SHA-256 校验（E8001）、诊断包脱敏导出、便携模式、`pack.ps1` + release 工作流 |
| S1 | ✅ | UI 世界层骨架：`Launcher.Render`（自有 GL 加载）+ GL 世界层 + 沉浸式 Shell + 相机/输入路由，GLES 3.0 闸门与降级横幅 |

UI（B）：

- [x] **M2（B）任务中心**：左侧底部「任务」入口；任务列表（安装 / 修复 / 整合包）、状态胶囊、进度条（`Percent=-1` 走不确定态）、取消 / 重试 / 修复按钮；Core 任务落库并在启动时恢复，中断任务标记为可修复
- [x] **M3（B）运行体验**：实例卡片（首页「最近游戏」+ 实例页）、自绘 3D 皮肤预览（拖拽旋转 / 滚轮缩放 / 行走 / 披风开关）、实时日志（日志页「运行日志」面板）、历史日志（启动器日志 + 每次启动的游戏输出落盘到 `logs/game`，取尾部 400 行预览）
- [x] **M4（B）账户**：标题栏方形头像与首页账号卡展示活动账户（未登录回退 Steve）；账户页支持账户列表 / 设为当前 / 删除；离线（自定义用户名）、微软 Device Code（展示并复制设备码、打开验证链接、取消登录）、外置 authlib（服务器地址 + 用户名 + 密码，缺失提示 `E6005`）

## 已知未达标

- 首帧实测 **5.2–8.1s**，未达 1.5s 预算（开发态、沙箱内、未 R2R publish；需在 R2R + 冷/热缓存条件下重测）。
- 发布链路只验证了打包与本地校验，未做「旧版 → 新版」端到端升级实测。
- S2 命中测试（Spike）尚未开始。
- `PortalLayout` 左右位次使用 `(i+1)/2`，存在 0.5 偏移（第 2/3 扇不对称），已记录为 S2 前待修缺陷。

## 待办（下一步）

- 发布流程端到端验证：先打一个旧版本 tag 走 `release.yml`，再实测「旧版 → 新版」升级。
- UI 剩余页面（安装/导入向导、更新入口等），按 `ui.md` 的 S2–S6 推进。
- 协作收尾：账号稳定后把 deploy key 降为 CI 只读。

## 最近进度

- **2026-10-27 · M4 账户（B）接入**：新增账户页（账户列表 / 设为当前 / 删除），三种登录方式（离线自定义用户名、微软 Device Code 展示与复制设备码 / 打开验证链接 / 取消、外置 authlib `E6005` 校验）；标题栏方形头像与首页账号卡改为点击进入账户页并实时展示活动账户，未登录回退 Steve；Fake 门面补齐账户增删改与微软演示流程；全量 154 项测试通过。
- **2026-10-27 · M3 运行体验（B）合入**：首页按视觉稿还原（账号卡 / 皮肤卡 / 最近游戏 / 260×66 大启动按钮）；新增自绘 `SkinPreview`（正交投影 + 仿射贴图，自动识别 64×64 / 64×32，披风与帽子层、行走、拖拽旋转、滚轮缩放）；实时日志改为日志页「运行日志」面板，并由 `GameLogRecorder` 落盘 `logs/game`；实例页改为卡片（启动 / 停止 / 修复 / 目录 / 版本设置）；窗口恢复 820×560 直窗（图标导航 + 方形皮肤头像标题栏），不再挂载沉浸式传送门 Shell。
- **2026-10-25 · 收尾修复**：接入 ClassicShell 切换（F2 / 左上角「经典视图」按钮）；Forge 模组按 `mods.toml` 的 `modId` 匹配启停并补测试；全量 142 项测试通过。
- **2026-10-25 · 合并 origin/main（M1–M3 UI）**：沉浸式 S1 保持默认视图，经典 PCL 布局原样迁入 `Shell/ClassicShell`；门面合并账户/任务/在线模组能力，CLI 同时保留 `loaders` 与 `loader-versions`；模组读写统一走实例真实 gameDir。
- **2026-10-25 · 仓库审议修复**：README 拆分当前状态与历史归档；technoledge/ui/第三方声明口径统一；新增 `docs-lint` + PR CI；补 `Launcher.Render.Tests`（19 项）；启动失败指引改为真实日志路径；格式门禁落地（133 项测试全通过）。

## 历史归档

- [`docs/history/README-archive-2026-10.md`](docs/history/README-archive-2026-10.md) —— 原始调研结论、历次文档复审原文、M0–M8 验收表、逐条进度日志。

## 文档索引

- [`technoledge.md`](technoledge.md) —— 技术方案、契约、目录与协作规则（当前事实源）
- [`ui.md`](ui.md) —— UI 层实施依据（S1–S6 任务与验收标准）
- [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md) —— 第三方组件与许可证
- [`LICENSE`](LICENSE) —— MIT

## 快速上手

见 `technoledge.md` §15「上手 Checklist」；首次构建前先设置 `AVALONIA_TELEMETRY_OPTOUT=1`，再执行 `dotnet restore` / `dotnet build -warnaserror` / `dotnet test`。
