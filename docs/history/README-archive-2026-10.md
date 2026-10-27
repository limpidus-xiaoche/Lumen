本文件为历史归档，内容逐字保留；当前状态见仓库根目录 README.md。

## 进度

- [x] 三个目标的文件/版本信息
- [x] PE 结构解析（`_probe/pe_probe.py`）
- [x] 字符串特征扫描（`_probe/str_probe.py`）
- [x] PCL2：.NET 程序集反射、内嵌资源清单、运行时目录、真实启动命令行
- [x] HMCL：自解压 ZIP 结构、内嵌 jar 清单、HMCLauncher 引导器分析
- [x] VersePC：app.asar 解析、主进程/后端代码抽取、插件与 AI 模块
- [x] 汇总对比与实现建议（见对话中的调研报告）
- [x] 完整技术结构规划方案（技术栈、分层架构、核心子系统算法、里程碑、验收标准）
- [x] 并行开发协作规范 `technoledge.md`（契约 v0.1、目录归属、PR 流程、M0–M8 分工）
- [x] `technoledge.md` v0.1 审查（2026-10-24）：P0 8 项 / P1 8 项 / P2 12 项，详见下方「文档审查记录」
- [x] `technoledge.md` 升版 **v0.2** 并回写：P0/P1/P2 全部落位（逐项对照见文档 §17 评审响应表）
- [x] 补充 §12.1 完整性校验（v0.3 定稿为 SHA-256 + fail-closed；曾拟公钥签名，已按决策取消）
- [x] **决策定稿 v0.3**：代号 `Lumen`；A=核心 / B=UI 保持；GitHub 私有 + SSH 公钥；**不签名，仅 SHA-256 哈希校验**；本项目 **MIT**；Forge ≤1.12.2 使用 `bangbang93.ForgeInstaller`（许可证为发布门禁）
- [x] `technoledge.md` **v0.4**：数据目录拆分（Roaming/Local）、argfile 竞态与编码、完整性校验时机、门面补口、CurseForge 决策项
- [x] CurseForge 方案拍板：**C**（Modrinth/HMCL/MultiMC；CF 仅直链包，M7 不验收 CF API）
- [x] GitHub 账号等级确认：**Free**（分支保护降级为约定 + pre-push hook + CI，CODEOWNERS 仅作 review 路由）
- [x] XMCL 评估（MIT，可 fork）：**放弃**；维持路线 C（.NET 10 + Avalonia + CmlLib.Core），XMCL 仅作 UX 参考
- [x] `technoledge.md` **v0.3 复审**（2026-10-24）：新增 P0 5 项 / P1 16 项 / P2 11 项，见下「v0.3 复审记录」
- [x] `technoledge.md` 升版 **v0.4** 完成：P0-1..P0-5 全部落位（CF=C、GitHub=Free）
- [x] `technoledge.md` **v0.4 复审（两轮）**：第 1 轮 6 个 P0 + 第 2 轮 3 个新增 P0，见下「v0.4 复审记录」
- [x] `technoledge.md` 升版 **v0.5**：P0/P1 全部回写（CmlLib 边界、协作身份、许可存证、编码/源/任务/SSRF/lzma、DTO、孤儿进程、hook、发布形态、路径/GC）
- [ ] **临时**：使用仓库 deploy key 推送（`ttss4096` 账号暂不可用）；账号恢复后迁移到 collaborators + 用户级 SSH，deploy key 降为只读
- [x] **M0 完成**：解决方案 + Protocol/Core/App/CLI + SQLite v3 + 离线身份 + 六页 Avalonia 壳 + FakeLauncherFacade + 8 项测试 + pre-push hook 文件
  - **归属说明**：`src/Launcher.App/**` 与 `tests/Launcher.App.Tests/**` 由 **A 代搭最小脚手架**（六页 + Fake + 深色主题），由 **B 接管**；B 首次提交后 App 路径只由 B 修改。
- [ ] B 接管 App：在 `feat/ui-m0` 分支确认或重写脚手架，并在提交信息中说明
- [x] **M1（A）完成**：manifest（含 `latest` 解析）、version.json、rules、`inheritsFrom` 合并、Java 探测（32/64 注册表）
- [x] **M1（B）基础模型**：下载页接真实 manifest 列表、设置页接真实 Java 列表；B 负责优化定稿
- [x] **M2（A）完成**：下载引擎（分片/续传/SHA1/URL 安全）、Vanilla 安装、install journal、秒装、修复、CLI install/repair
- [ ] **M2（B）**：任务中心、安装进度 UI、取消/重试、启动恢复（B 负责）
- [x] **M3（A）完成**：参数拼装/变量展开、Java 选择、argfile 编码与生命周期、进程启动/日志、退出归因、运行会话、CLI launch/stop/sessions
- [ ] **M3（B）**：实例卡片、启动按钮、实时日志、历史日志（B 负责）
- [x] **M4（A）完成**：账户存储（DPAPI 整文件加密）、微软 Device Code + 自动刷新、authlib-injector 外置登录、启动凭据注入、CLI `login/accounts/use/logout/instances`
- [ ] **M4（B）**：账户页、登录流程、换机后重新登录引导（B 负责）
- [x] **M5（A）完成**：加载器版本清单（Fabric/Forge/NeoForge）、Fabric profile 合并安装、Forge/NeoForge 官方 installer headless 安装、本地版本解析、换加载器、CLI `loaders` / `install --loader`
- [ ] **M5（B）**：安装向导（版本 + 加载器 + 加载器版本列表）（B 负责）
- [x] **M6（A）完成**：`root_path` 作为 gameDir 权威、MultiMC/HMCL/原生实例复制导入、模组扫描与启停、删除保留存档、CLI `mods/mods-toggle/import/delete`
- [ ] **M6（B）**：实例管理页、模组列表、导入向导、删除确认（B 负责）
- [x] **M7（A）完成**：Modrinth / HMCL / MultiMC 整合包导入（CF 仅直链包）、清单依赖下载 + SHA1 校验、overrides 复制、CLI `modpack install`；实机验证 1.21.1 + Fabric 0.20.1 + Sodium 导入后可启动
- [ ] **M7（B）**：拖拽导入、依赖确认、结果页（B 负责）
- [x] **M8（A）完成**：`integrity.json` 快/全量校验（mtime 缓存，失败 E8002）、更新清单解析 + 流式 SHA-256 下载校验 + 版本单调（E8001）、诊断包脱敏导出、便携模式写探测与回退、CLI `integrity / update check|apply / diagnostics`、`scripts/pack.ps1` + `scripts/verify-integrity.ps1` + `.github/workflows/release.yml`（Velopack Setup.exe + Portable.zip，无 MSI）
- [ ] **M8（B）**：更新入口、首次启动引导、白名单帮助页、日志轮转设置（B 负责）
- [x] `ui.md` v1 草案复审（2026-10-24）：P0 7 / P1 12 / P2 10，见下「ui.md 复审记录」；待回写 v1.1 后再进 S1
- [x] `ui.md` 第二轮复审（**v1.1**，2026-10-24）：P0 4 / P1 12 / P2 10；v1 的 7 项 P0 已回写，见下「ui.md 复审记录（第二轮 · v1.1）」

## v0.4 复审记录（已回写 v0.5，保留追溯）

> 处理结果见 `technoledge.md` §17 的 v0.5 各行与 §13-21/22/23。

第 1 轮（已回写 v0.5）：

1. §6.5 L404 兜底「JDK ≤17 用 `Encoding.Default`（CP936）」在 .NET 5+ 不成立（`Encoding.Default` 恒为 UTF-8）→ 需 `CodePagesEncodingProvider` + `GetEncoding(ANSICodePage)`，并加包 `System.Text.Encoding.CodePages`
2. §6.5 L402 为读 `sun.jnu.encoding` 每次启动多起一个 JVM（100–300ms）→ 改按 JRE 大版本静态决定 + 按 java 路径缓存
3. §6.2 L343 `download.mcbbs.net`：MCBBS 已于 2024 年关站，需实测；源表应改为可下发的 `sources.json` 而非 Core 常量
4. §5 缺 `CancelTaskAsync(taskId)` / `RetryTaskAsync(taskId)`：`GetTasksAsync` 恢复的任务无 CT 持有者，实际无法取消
5. 整合包清单里的下载 URL 无约束 → SSRF/任意拉取；需仅 https + host 白名单 + 禁 localhost/`file:`
6. §6.4 L396 JRE 的 `lzma` 变体未提：System.IO.Compression 不支持 LZMA → 明确只取 `raw` 或引入 7z（含许可证）

第 2 轮（本轮新增内容：CmlLib.Core / PCL2 许可边界 / XMCL 否决 / 真实仓库）：

7. **CmlLib.Core 与自研内核的能力边界未定**：它自带下载/安装/启动/Java 逻辑，直接复用会绕过 §6.2 的 `.part.meta`、镜像健康度与 §6.3 的 install journal（M2「中断可继续」失效），也可能自行拼命令行（M3 argfile/wrapper 失效）→ 需能力边界表：只复用元数据解析与 natives/asset 提取；下载、参数拼装、进程管理必须留在 `Launcher.Core`
8. **Deploy key 无法支撑双人 PR 流程**：deploy key 是仓库级凭据，无法代表两个 GitHub 用户 → PR 作者/approve/CODEOWNERS 全部失义；Free 计划无分支保护 + 写权限 key = 可直推/强推 `main` → 改用两个协作者账号
9. **PCL2 许可证结论是转述**：§12 的「名称 PCL 开头 / 蓝色主色相 / 关于页署名」属法律判断，需附许可原文链接 + 存档日期 + 本地副本，否则不可核验

第 1 轮 P1（未回写）：DTO 字段全缺（`LauncherSnapshot/InstanceSummary/InstanceDetail/AccountSummary/ModEntry/ModPackInstallRequest/AppSettingsPatch`）；`StopGameAsync(instanceId)` 无法处理孤儿进程（缺 `GetRunningSessionsAsync`）；§9.0 pre-push hook 无安装步骤（§15 缺 `core.hooksPath`）；authlib-injector SHA1 无基准（需内置 pin 表）；`integrity.json` 未写 CI 构建时生成；发布形态（self-contained/R2R/单文件/裁剪）未定；外部导入「复制 vs 原地接管」未定；路径安全校验缺失；`blobs` 无 GC；§10 M0 仍写「SQLite v2 / 契约 v0.2」；§1 仍列 CurseForge；§6.4「v1 只接受 x64」措辞冲突；§11 矩阵含非法组合。

## v0.3 复审记录（已回写 v0.4，保留追溯）

> 处理结果见 `technoledge.md` §17 的 v0.4 各行；以下为原始复审条目。

P0（会造成实际故障或阻断里程碑）：

1. `%APPDATA%\Lumen` 存放数 GB 的 versions/libraries/assets → 域环境漫游配置会同步爆炸，应改 `%LOCALAPPDATA%`
2. argfile「`Process.Start` 返回后立即删除」存在竞态（JVM 可能尚未读取即被删）→ 改为观察到首行输出后删除 + 失败重试 + 启动时清扫
3. argfile 编码必须用系统 ANSI（中文 Windows = GBK）：Java 9–17 不按 UTF-8 解析该文件，否则中文/空格路径乱码，M3「中文路径」验收必挂
4. §12.1 每次启动全量 SHA-256 校验自身文件 vs §6.10「冷启动 ≤1.5s」冲突（self-contained 约 100MB+）→ 改为「版本变更后校验」或关键文件子集 + 校验记录
5. CurseForge 需 API key 才能解析整合包 `projectID/fileID`，文档未定义来源与配置策略 → M7 硬阻塞

P1：门面仍缺 `StopGame/GetInstanceAsync/GetTasksAsync`、`ImportExternalAsync` 无进度、`SetActiveAccount`、`LoginRequest` 无 Offline 模式；Progress 回调线程与事件节流未定义；镜像源列表与优先级未定义（B 的「下载源」无数据来源）；分片多源混下无法归因校验失败；「秒装」全量 SHA1 与 `blobs.verified_at` 快路径矛盾；1.13+ 必须 64 位 JRE 未写；内存/JVM 默认策略缺失；Forge ≤1.12 回退官方 installer 路径未验证；Quilt 范围不一致；Protocol 目录缺 `Loaders/Settings`；`NuGet.config` 国内镜像与 GitHub CI 冲突；DB 备份前需 `wal_checkpoint`；`download_queue` 缺 `instance_id/task_id/attempts`；日志 sink 与 CLI 解析器选型未定；私有仓分支保护/CODEOWNERS 强制需确认计划；§17 残留「云 HSM」与 v0.3「不签名」矛盾，版本号标记混乱（标题 v0.3 / 各节 v0.2 / 契约表缺 v0.3）。

P2：CF/HMCL/MultiMC 包内文件名未明确、依赖补齐能力被高估、`E5003` 内部兜底不应作错误码、`0xC0000005` 为负 int 需按无符号比较、崩溃报告不入日志轮转、便携模式写权限回退、`None` 隔离 gameDir 未写死、`instance.json` 与 DB 权威未定义、升级 mods 兼容提示、测试缺项（代理/E2004/便携/换机 DPAPI/单实例锁/DTO 源生成覆盖/3000 asset 压力）、错误码 i18n key、第三方表缺「是否随包」列。

## 文档审查记录（technoledge.md v0.1 → v0.2，已全部处理）

> 处理结果逐项对照见 `technoledge.md` §17；以下保留原始审查条目用于追溯。

P0（阻塞并行开发，必须先改文档）：

1. 跨平台宣称 vs Windows-only 实现（DPAPI / 注册表 / MSI / Authenticode / @argfile）→ 明确 v1 仅 Windows，或抽象 `ICredentialProtector`
2. `ILauncherFacade` 缺 7 类能力（模组启停、实例编辑/隔离、加载器版本列表、整合包安装、设置写入、更新检查、历史日志）→ M5–M8 无法按文档实现
3. `@argfile` 把 accessToken 明文落盘 → 违反「禁止明文落盘」；诊断包需排除
4. `Launcher.Core` 禁用容器 vs `Microsoft.Extensions.Http.Resilience`（DI 中心）→ 改用 Polly `ResiliencePipeline`
5. 分发矛盾：Velopack（Setup.exe 增量）vs MSI；签名证书 2023 起需 HSM/云签名
6. 里程碑依赖倒置：M3 验收要离线登录，但离线认证在 M4
7. 分片下载策略与真实网络行为冲突（4MB 分片对小 asset、SHA1 不可并行分段、429 降权、缺 `.part` 元数据）
8. authlib-injector（AGPL-3.0，需复核）+ Minecraft EULA 合规风险 → 改运行时下载 + 合规声明

P1（一致性）：`${...}` 断言与 legacy 参数冲突、Protocol 规则与门面位置矛盾、进度日志双源（README vs §14）、.NET 8/10「API 兼容」说法、Forge 1.13–1.16.5 安装路径缺失、测试矩阵缺 Java 16、CLI `--offline/--dir` 语义不一致、`tests/Core.Tests` 归属矛盾。

P2（工程细节）：DB 缺字段/迁移、无安装事务与实例状态机、缺代理/超时/重试/磁盘预检/单实例锁/队列持久化、缺 `.editorconfig`/`global.json`/`NuGet.config`/`.config/dotnet-tools.json`、非 ASCII 路径与 longpaths、注册表 32/64 视图、隔离语义与 natives 归属、删除实例保留存档、命名与 VersePC 撞车、性能预算/日志轮转/错误码、第三方组件表缺项、`file_cache` 内容寻址设计、`Percent` 语义（未知总量）。

## 规划定稿（速查）

- 代号：`Lumen`（发布名待定，避开 VersePC 前缀；发布前做商标检索）。
- 平台：**v1 仅 Windows 10/11 x64**；跨平台列入 v2 非目标（DPAPI / 注册表 / 签名均为平台相关）。
- 技术栈：`.NET 10 LTS`（环境不一致则统一 `.NET 8 LTS`，单一 TFM 由 `global.json` 钉死）+ `Avalonia 12.1.4` + `CommunityToolkit.Mvvm` + `Polly.Core`；`System.Text.Json` 源生成；SQLite 存索引，JSON 存配置。
- 形态：单进程 GUI + 可复用 `Launcher.Core` 类库 + `Launcher.Cli`（内核可被 CLI/测试直接驱动）；`Core` 不引用任何 UI 类型。
- 规避 32K 命令行：Java 9+ 用 `@argfile`（**等首行 stdout 后删除** + ACL 收紧）；Java 8 回退 wrapper jar（对标 PCL2 JavaWrapper）。
- Forge/NeoForge：≤1.12.2 使用 `com.bangbang93.ForgeInstaller`（**许可证核验为发布门禁**），1.13+ 走官方 installer；`authlib-injector` 不随包分发。分发只用 Velopack（`Setup.exe` + delta + Portable zip），不产 MSI。
- 完整性：**不签名**（无 Authenticode、无应用层公钥签名），只用 **SHA-256 清单**（自身 `integrity.json` + 更新清单）并 fail-closed；安全边界与残余风险见 technoledge §12.1。
- 仓库：**GitHub 私有**，两位开发者各自 SSH 公钥（ed25519），`main` 保护 + 对方 1 approve，CODEOWNERS 按 A/B 目录划分。
- 复用策略：优先评估 **CmlLib.Core（MIT）** 覆盖元数据/下载/启动原语；**不 fork XMCL / PCL2**，XMCL 仅作 UX 参考。
- 许可证：本项目 **MIT**；随包第三方必须与 MIT 兼容，不兼容者仅运行期下载/调用，并登记 `THIRD_PARTY_NOTICES.md`。
- v1 范围：元数据 → 安装 → 启动 → Java 管理 → 离线/微软/外置登录 → 实例隔离 → 模组/整合包 → 自更新。AI/联机/皮肤站作为 v2。

## M0 验收结果（2026-10-24）

| 项 | 结果 |
|---|---|
| `dotnet build` | 0 警告 / 0 错误（SDK 10.0.300，`net10.0-windows`） |
| `dotnet test` | 8 项通过（Core 5 / App 2 / Integration 1） |
| CLI `offline-uuid --name Player` | `a01e3843-e521-3998-958a-f459800e4d11` |
| CLI `paths` | `%APPDATA%\Lumen` / `%LOCALAPPDATA%\Lumen` 已拆分 |
| GUI 冒烟 | `Lumen.exe` 启动并保持运行；日志写入 `%LOCALAPPDATA%\Lumen\logs` |
| 架构测试 | Core 不依赖 Avalonia / Hosting / DI |
| pre-push hook | 文件已提交，待执行 `git config core.hooksPath .githooks` 启用 |
| App 归属 | A 代搭脚手架；B 接管；后续基础模型由 A 提供、B 优化 |

## M1 验收结果（A 部分，2026-10-24）

| 项 | 结果 |
|---|---|
| `versions --limit 5` | `latest_release=26.3` / `latest_snapshot=26.4-snapshot-5`，清单解析正确 |
| `resolve 1.21.1` | mainClass `net.minecraft.client.main.Main`；97 个库；28 game args；11 jvm args；assetIndex 17 |
| `java` | 检出 6 个 JDK（Oracle 25/21、Temurin 21/17、Microsoft 21，全部 x64） |
| 测试 | **28 项通过**（Core 23 / App 4 / Integration 1） |
| 构建 | 0 警告 / 0 错误 |
| 前端基础模型 | 下载页接 `RefreshManifestAsync`（真实版本列表）；设置页接 `GetSnapshotAsync`（Java 列表）；App 启动使用真实 `LauncherFacade` |
| GUI 冒烟（真实 Core） | `Lumen.exe` 启动，SQLite 初始化成功，日志无错误 |

> B 部分只做了基础模型（数据绑定 + 列表），视觉与交互仍由 B 优化定稿。

## M2 验收结果（A 部分，2026-10-24）

| 项 | 结果 |
|---|---|
| `install 1.21.1`（完整资源） | 成功；70 个库；**787.6 MB** 资源；客户端 jar SHA1 与 manifest 一致 |
| `install 1.12.2`（完整资源） | 成功；1290 个 asset、105 个库文件、11 个 native；客户端 SHA1 一致 |
| 秒装 | 重复安装 1.12.2（含资源）**1.61s**；1.21.1（跳过资源）**1.14s** |
| 修复 | 删除 `patchy-1.3.9.jar` 后 `repair test-1.12.2`，**3.78s** 恢复 |
| 测试 | **35 项通过**（Core 30 / App 4 / Integration 1） |
| 构建 | 0 警告 / 0 错误 |
| 安全 | URL 校验：仅 https、host 白名单、私网/环回拒绝、重定向逐跳复检；单测覆盖 |

> B 部分（任务中心 UI、进度展示、取消/重试按钮、启动恢复）尚未做；Core 已提供 `TaskRegistry` 与 `CancelTaskAsync/RetryTaskAsync`。
> 断点续传逻辑已实现（`.part` + `.part.meta` + 分片文件），但本轮未做"中途强杀后续传"的实测。

## M3 验收结果（A 部分，2026-10-24）

| 项 | 结果 |
|---|---|
| 真实启动 | `launch test-1.21.1 --username Player` 成功；游戏进入主菜单（日志出现 atlas 创建） |
| 运行会话 | `sessions` 显示 `pid=83128`；`stop` 后输出"没有运行中的游戏" |
| 停止 | `stop` 使用 Graceful，游戏打印 `Stopping!`，退出码 **0** |
| 日志 | stdout/stderr 实时回传；退出归因已接（崩溃报告/退出码） |
| 测试 | **50 项通过**（Core 45 / App 4 / Integration 1） |
| 构建 | 0 警告 / 0 错误 |
| 默认语言 | 安装/首次启动前写入 `options.txt` `lang:zh_cn`；实机启动并退出后仍为 `zh_cn` |
| 已知 | 1.21.1 命令行未超 24K，真实 argfile 启动未覆盖；Java 8 超长命令行未实现 wrapper（报 `E5003`） |

> 离线账号的 Realms 认证会报 `Failed to parse into SignedJWT`，属于预期（没有正版 token），不影响本地游戏。

## M7 验收结果（A 部分，2026-10-24）

| 项 | 结果 |
|---|---|
| 整合包格式 | Modrinth（`modrinth.index.json`）、HMCL（`modpack.json`）、MultiMC（`mmc-pack.json`）、CF 直链（`manifest.json`，仅 overrides，不调 CF API） |
| 识别 | 按文件名写死，未知格式抛 `E3004` |
| 安装 | 解析依赖（MC 版本 + 加载器/版本）→ 复用 Vanilla/加载器安装 → 下载清单文件（SHA1 校验）→ 复制 overrides |
| 容错 | 无 SHA1 且非白名单直链**跳过**而非失败，避免单个坏文件阻断整包 |
| CLI | `modpack install <path> [--name]`；`--portable` 在 `System.CommandLine` 之前预过滤 |
| 测试 | **91 项通过**（Core 86 / App 4 / Integration 1） |
| 实机 | Modrinth 目录包与 `.mrpack` 压缩包导入成功；Sodium 下载校验正确；overrides 复制正常；默认语言 `zh_cn` 生效；启动无错误 |
| 构建 | 0 警告 / 0 错误 |

> CurseForge 包只做 overrides 导入，不解析 `projectID/fileID` 依赖（§6.3.1 决策 C）。
> B 部分（拖拽导入、依赖确认、结果页）尚未做。

## M8 验收结果（A 部分，2026-10-24）

| 项 | 结果 |
|---|---|
| 完整性清单 | `integrity.json`（`version/algorithm/generatedAt/files[{path,size,sha256}]`），由 `pack.ps1` 在 CI/本地构建阶段生成 |
| 快校验 | 只校验入口 exe + 清单本身；按「版本 + mtime 汇总」缓存到 `cache/integrity-check.json`，命中直接通过 |
| 全量校验 | 逐文件 SHA-256；失败 `E8002` 立即拒绝运行/自我替换 |
| 更新清单 | `releases.<channel>.json` 固定字段，未知字段忽略；缺 version/url/sha256/size 即 `E8001`；默认拒绝降级（`--force` 可越级） |
| 更新包校验 | 先 size 后流式 SHA-256（`IncrementalHash`），不符重试 2 次后 `E8001` |
| 更新源安全 | `UrlGuard`：仅 https + 443 + host 白名单，拒私网/环回 |
| 诊断包 | `%LOCALAPPDATA%\Lumen\exports\diagnostics-*.zip`；排除 `accounts.dat`/`args.txt`/`.part.meta`；日志 token/Bearer/URL 凭据脱敏，用户目录替换 `%USERPROFILE%` |
| 便携模式 | `--portable` 写探测 exe 同级 `data\`；不可写回退 `%LOCALAPPDATA%\Lumen` 并告警（`portable_fell_back=true`） |
| 打包 | `pack.ps1`：publish(242 文件) → integrity 校验 → `vpk pack` → Setup.exe(68,433,392 B) + Portable.zip(60,918,322 B) + nupkg；断言无 MSI |
| 实机 | `--portable integrity` 覆盖 1 个入口文件；`integrity --full` 覆盖 52 文件；同长度篡改 DLL → `E8002` exit 1；`update check` 无发布 → `E8001` HTTP 404；`diagnostics` 产出 6 条目 zip |
| 测试 | **114 项通过**（Core 109 / App 4 / Integration 1） |
| 构建 | 0 警告 / 0 错误 |

> 决策：Velopack 原生 feed 重命名为 `velopack.<channel>.json`，避免与 §12.1 固定字段的 `releases.<channel>.json` 冲突；Lumen 不消费 Velopack feed，直接按固定清单下载 Setup.exe 并校验后启动。
> 未做：真实「旧版升级到新版」端到端流程（需先发布一个旧 tag）；更新 UI 属 B 部分。

## ui.md 复审记录（v1 草案，2026-10-24）

> 结论：方向（世界层 + 2D 面板 + 可回退经典模式）可执行，S0 已把最大不确定性（CompositionVisual 3D API、Blender headless）探掉；
> 但 7 项 P0 会让验收直接失败或产生结构性返工，建议先出 `ui.md` v1.1 再进 S1。
>
> 已核实（本机程序集/包元数据）：
> `Avalonia 12.1.4` 含 `CompositionVisual` / `ElementComposition` / `GetElementVisual` / `OpenGlControlBase` / `GlInterface`；
> `SharpGLTF.Core 1.0.5`（MIT，net8.0 无额外依赖，不引 Newtonsoft）；`NAudio 3.0.1` + `NAudio.Vorbis 3.0.0`（MIT，net9.0，需 NAudio 3 拆包）；
> `StbImageSharp`（`Unlicense OR MIT`）。均与 MIT 兼容，但必须登记 `THIRD_PARTY_NOTICES.md`。

P0（阻断验收 / 结构性返工）：

1. **ESC 三重语义 + 无边框回退死路**：P1「ESC 退出全屏」、S2「Esc 返回」、S3「Esc 关闭面板」冲突；`SystemDecorations=None` 常驻时退出全屏会得到不可移动/不可关闭的窗口。需定义 ESC 优先级（模态 > 面板 > 全屏）并让装饰随窗口状态切换。
2. **状态源缺「恢复」且无法表达并发**：S4 只绑进程内 `IProgress`，未使用 `GetTasksAsync` / `GetRunningSessionsAsync`（均已存在），重启后安装/运行态丢失；单一 `SceneState` 无法同时表达「两扇门在安装、一扇在运行、一扇报错」。应改为「每门实例态 + 全局态派生」。
3. **3D 面板/图标的命中测试未验证**：`CompositionVisual` 的 `Offset/Scale/Orientation` 属合成层属性，Avalonia 命中测试基于 Visual 变换与布局边界，按钮可能点不到；且位移后会被父级 `ClipToBounds` 裁切。需在 S2/S3 前做 spike，回退方案：`RenderTransform`（参与命中测试）+ `ZIndex` 排序。
4. **降级矩阵写了不存在的 API**：`SystemParameters.IsRemoteSession` 是 WPF，Avalonia 无 `SystemParameters`（改用 `GetSystemMetrics(SM_REMOTESESSION)`）；软件渲染下 `OpenGlControlBase` 根本起不来，不可能「≥20fps」，应统一归入「GL 不可用 → 静态背景」。`GC.GetGCMemoryInfo()` 也不等于物理内存（用 `GlobalMemoryStatusEx`）。
5. **UI 专属设置项缺位，与「Core 不改一行」冲突**：`ClassicMode` / 全屏窗口模式 / `Enable3DHero` / 音量 / 减少动效在 [SettingsModels.cs](file:///D:/启动器/src/Launcher.Protocol/Settings/SettingsModels.cs#L17-L40) 的 `AppSettings` 与 `AppSettingsPatch` 中都不存在。要么新增 App 级 `ui.json`，要么改 Protocol（并撤回「Core 不改一行」）。
6. **验收指标自相矛盾/不可测**：包体基线 `68MB` 是 Setup.exe（Portable.zip 60.9MB，publish 242 文件），不能当发布目录基线；「内存增量 ≤120MB」与 technoledge §6.10「UI 空闲内存 <150MB」冲突；核显无法测「显存 <300MB」；「空闲 CPU <3%」与「8s 公转 + 粒子常驻」互斥。
7. **阶段范围与 M2–M8（B）不对齐**：S3 只列 5 个面板，缺任务中心、更新入口、安装向导、导入向导、首次引导、诊断/完整性阻断页；而 §3.3 又说日志「切经典 2D 页」，自相矛盾。照此实施会漏掉 B 侧已承诺的验收项。

P1（文档一致性 / 关键技术细节）：

1. §0 冲突裁决写「technoledge.md §7（UI 规范）」——§7 实为数据与文件布局，UI 规范在 §6.10；条款失效且未声明是否覆盖 §6.10 的性能预算。
2. 图层顺序错误：`L1 后处理遮罩（纯 2D）`必须在世界层之上、面板层之下，否则暗角/色差无法作用于 GL 画面（应插在 L2 与 L3 之间）。
3. `L4 Tooltip/弹窗层 IsHitTestVisible=false` 与「弹窗」职责冲突，确认框/登录码需要可交互层，应拆 `TooltipLayer`（穿透）与 `ModalLayer`（可交互）。
4. 「世界层与面板层共用同一 `Camera`/`ViewProjection`」不可实现：`CompositionVisual` 无投影矩阵，只能共用相机参数后在 CPU 复算每面板 TRS + `scale=focal/z`。
5. 文字清晰只覆盖了 GL，未覆盖**合成层缩放/旋转**：控件先按原始尺寸栅格化再变换，放大与倾斜会发虚。应约束面板 `scale∈[0.9,1.1]`、以位移为主，或 2x 离屏栅格化（并计入内存）。
6. DPI/多显示器全缺：GL 用物理像素、面板用 DIP、`RayPicker` 必须乘 `RenderScaling`；全屏落在哪块屏、窗口模式记忆位置也未定义。
7. 启动时序算术不成立：推镜 600ms + 白闪 250ms 后才 `LaunchAsync`，却要求「点击到进程启动 ≤1.2s」，JVM/凭据刷新预算只剩 350ms。应并行触发或把指标放宽到 2s。
8. 两套 GL 绑定未划界：`Silk.NET.OpenGL` 与 `OpenGlControlBase` 的 `GlInterface` 会各自加载入口点；且 Windows 下是 ANGLE/GLES（GLSL ES、无桌面 GL 独占特性），需明确只保留一套，或由 App 用 `GetProcAddress` 注入。
9. 「永不黑屏」未覆盖运行期故障：驱动更新/TDR/GL 上下文丢失、初始化成功后崩溃，需重建上下文或直接降级。
10. 音频依赖与预算：`NAudio` 必须钉 3.x 并显式引 `NAudio.Wasapi`（`WasapiOut` 所在），`NAudio.Vorbis 3.0` 只配 NAudio 3；3MB Ogg(q4,≈128kbps) 只有 2–3 分钟，环境循环会明显重复；`ffmpeg/libvorbis` 是构建期外部依赖，需写明且不入包。
11. 隐私与行为缺失：`--demo`/截图模式未脱敏账户名与路径（与 `DiagnosticsExporter` 标准不一致）；README 早前决定的「失焦/最小化停渲染」未落到阶段任务；游戏 Running 时 BGM 是继续还是淡出未定义。
12. A/B 边界自相矛盾：§7 说 `App/Shell|World|Panels|Audio` 归 B，S1 任务却全是 App 改动；与「自 B 首次提交起 A 不再改 App」冲突。需明确 S1 由谁执行（A 只交付 `Launcher.Render` + 骨架草案）。

P2（工程细节）：

1. 门布局参数：`Opacity = 0.9 - 0.12*index` 到第 7 扇为 0.18（近乎不可见），需 clamp；7/8 扇的「更多」边界要写清；S1 验收「符合 3.2 规则」应为 3.1。
2. `RayPicker` 用 AABB 对 22° 旋转门不精确，应改 OBB 或包围球。
3. 实例图标纹理规格缺失：尺寸上限、缓存、非 2 次幂、缺失图标回退。
4. 「更多」列表需虚拟化（对齐 technoledge「50 实例 ≤200ms」）。
5. `CompositionVisual` 每帧改属性 vs 用合成动画未定；面板重挂载后需重新获取 visual 并重放变换。
6. 缺单实例二次启动激活、窗口位置记忆、关闭/最小化/关于/许可/第三方声明入口。
7. 可测性埋点不足：需要 `LUMEN_DISABLE_GL=1` 强制降级、`--demo` 脚本、帧时间采样导出，否则 S1/S6 验收靠肉眼。
8. 缺单测清单：`PortalLayout`（纯函数）、`RayPicker`、`CameraController`、`SceneStateMachine` 映射；S6 只有回归脚本。
9. 构建细节：新包必须进 [Directory.Packages.props](file:///D:/启动器/Directory.Packages.props)（中央版本管理），`Launcher.Render` 继承 `net10.0-windows` 且 `TreatWarningsAsErrors=true`；S1 无外部资产时不必引 `SharpGLTF.Toolkit/Runtime`。
10. 无障碍/减少动效：3D 门的键盘焦点可视化与 `AutomationProperties` 缺失；白闪与报错脉冲应受「减少动效」约束；运行中实例应禁用删除/改版本（项目硬约束）。

## ui.md 复审记录（第二轮 · v1.1，2026-10-24）

> 审查对象已是 v1.1：v1 的 7 项 P0 全部回写（ESC 返回栈 + 装饰恢复、每门状态 + 重启恢复、命中测试策略 + Spike、真实降级 API、设置字段扩展、指标基线、11 面板、图层顺序、GlInterface/GLSL ES、上下文丢失、NAudio 版本、DPI）。
> 本轮基于 Avalonia 12.1.4 源码/官方文档与现有 App 代码复核，仍有 **P0 4 / P1 12 / P2 10**。

P0（仍会阻断 S1/S6）：

1. **门到底由谁渲染，自相矛盾**：§3.2 写“传送门/图标 = `CompositionVisual` 3D + 自定义射线拾取”，但 S1 任务 5/7 在 `WorldView(OpenGlControlBase)` 里画门框/门内，§8.2 门内还需要“流动噪声/星尘”shader。门若走合成层，就没有 GL 深度/雾/光照，门内 shader 无处安放；门若走 GL，§3.2 表格、图标、键盘焦点与 `AutomationProperties` 都要重写。必须二选一并全文统一。
2. **截图链路仍不成立**：§1.4/S6 要求按物理像素导出 2560×1440。已核对 Avalonia 源码：`OpenGlControlBase` 把 GL 画面挂成 composition **child visual**（`ElementComposition.SetElementChildVisual`）；官方文档明确 `RenderTargetBitmap` 为软件渲染、GPU 路径“可能无法正确渲染”。必须改为 `glReadPixels` + 面板离屏合成，或屏幕捕获再缩放。§3.4 用 `RenderTargetBitmap` 做面板 2× 可以，但那只覆盖面板，不覆盖世界层。
3. **Avalonia 12 窗口 API 未对齐**：§1.1 表与代码仍用 `SystemDecorations`；12 的公开属性是 `WindowDecorations`（Full/BorderOnly/None），`SystemDecorations` 已不在窗口属性文档（二进制仍有符号，疑为兼容/Obsolete）。项目 `TreatWarningsAsErrors=true`，若确为 Obsolete，S1 第 2 条任务即构建失败；另 `WindowState` 已变 direct property，不能用样式驱动全屏切换。
4. **平台渲染后端未锁定**：`OpenGlControlBase` 依赖 `TryCreateCompatibleGlContextAsync`（compositor 的 GL 互操作）；Win32 后端有 `AngleEgl`/`Wgl`/`Vulkan`/`Software`，当前 [Program.cs](file:///D:/启动器/src/Launcher.App/Program.cs#L56-L60) 只调 `UsePlatformDetect()`。若落到 Vulkan/Software，世界层必失败。§3.5 断言“Windows 走 ANGLE/GLES”不等于配置；需显式设 `Win32PlatformOptions.RenderingMode` 并保留能力探测。

P1（关键细节）：

1. **`Launcher.Render` 与 `GlInterface` 冲突**：§10 说 `Launcher.Render` 不引 Avalonia，§3.5 说只用 Avalonia 的 `GlInterface`——后者是 `Avalonia.OpenGL` 类型。需改成“`Render` 只做数学/场景，GL 调用经 `IGlApi`/函数指针由 App 注入”，或允许 Render 依赖 `Avalonia.OpenGL`。
2. **§1.4 的源码事实错误**：`OnOpenGlRender(GlInterface gl, int fb)` 没有 w/h 参数，“`RenderScaling` 已计入 w/h”不成立；像素尺寸需自行算 `Bounds × TopLevel.RenderScaling`（源码内部 `GetPixelSize` 同法）。`ctx.IsLost` 也不可访问，应改用 `OnOpenGlLost` 回调或 `glGetError() == GL_CONTEXT_LOST`。
3. **§3.3 投影代码单位混用**：`focal` 用 `heightPx`，`screen` 返回 DIP，`RenderTransform` 也是 DIP；且 `RenderTransformOrigin` 默认是中心，`Matrix.CreateScale * Translate` 会整体偏移半个面板。需统一 DIP 并把 `RenderTransformOrigin` 设为 `TopLeft`。
4. **仿射变换无法“倾斜”**：§3.2/§3.4 规定面板只用仿射 `RenderTransform`，但 S4 验收仍写“面板倾斜 ≤15° 时正文可读”。要么删掉倾斜要求（全部 billboard），要么明确是 2D skew 而非 3D 倾斜。
5. **§3.6 的“1s 无帧 = 上下文丢失”会误判**：S1 任务 8 要求失焦/最小化停渲染、空闲 30fps，暂停超过 1s 必触发“丢失→重建→降级”。计时必须只在“窗口可见 + 已请求渲染”时进行。
6. **§4.2 启动第 4 步不可行**：`GetInstanceAsync` 只读 DB 详情，不做版本目录校验；对每个实例做目录完整性校验会击穿 1.5s。应读 DB `state` 字段，深度校验放到打开实例面板/修复时。
7. **相机默认值仍缺**：有 `Camera` 结构但没有位置、FOV、near/far 与场景单位；22°/2.2/0.35 与 `focal` 无法复现，S1“位置/缩放/不透明度符合 §8.1”不可计算。§14 未决 4 仍是 4:3 安全区，但 S4 已要求 4:3 不裁切，S6 又计划竖版。
8. **首帧关键路径仍未定义**：manifest 刷新、更新检查、完整性快校验、音频初始化没有“延后 + 可取消”规则；[App.axaml.cs](file:///D:/启动器/src/Launcher.App/App.axaml.cs#L21-L38) 仍在 UI 线程同步初始化 Core/DB，1.5s 预算无法保证。
9. **Core 失败静默回退 Fake**：同文件 L24-32 初始化失败就切 `FakeLauncherFacade`，界面照常显示假实例/假进度；S1/S3 验收会被假数据骗过。需要显式“降级模式”横幅并禁用写操作。
10. **音频设备失败无兜底**：RDP/无声卡/被占用时 `WasapiOut` 初始化失败，不能让 S5“首帧不受影响”变成异常；需静默降级 + 设备热插拔。
11. **快捷键作用域仍只覆盖 ESC**：§1.2 没定义输入框/ComboBox/IME 组合态下 `Space`/`Enter`/`Tab` 的优先级（Space 既会打字也会启动门）；Avalonia 12 的 AccessKey 语义也已改变。
12. **§11 取消 A/B 与既有协作规则冲突**：README 待办、`technoledge.md` 分工、项目记忆里仍有“App 仅由 B 修改”。需同步更新，否则其他 Agent 会按旧规则拒绝改 App。另 `Launcher.App.csproj` 仍设 `AvaloniaUseCompiledBindingsByDefault=false`（12 默认 true），11 个面板的绑定会退回反射。

P2（工程细节）：

1. 全文 `technoledg.md` 拼写错误（7 处），实际文件是 `technoledge.md`，会误导其他 Agent。
2. §5 诊断与关于放在“石碑背面”不可发现，设置面板里也要有入口。
3. §6.1 `EnableEnhanced3D` 注释“B 层”是 v1 遗留概念，与 §11 取消 A/B 冲突。
4. `AppSettings.AccentColor`（默认蓝）与固定 `--accent #8B5CF6` 双源未处理。
5. `--demo` 用真实数据且全自动，可能真实启动游戏/写实例；脱敏按用户决定不做，但应至少限定只读快照或显式确认。
6. §3.6 自造 `E9001` 未进错误码表（technoledge 要求码 + i18n key）。
7. §12.1 缺 `InputRouter` 返回栈、窗口状态机/位置钳制、PanelHost 投影数学、音频状态机、设置迁移的单测。
8. §13 “ANGE” 拼写；CI 静态检查 GLSL 没有落地方案（需 glslangValidator/ANGLE 工具链）。
9. §9.1 的空闲 30fps / CPU <5% 没有对应验收项（S1/S6 只测 60fps 与失焦）。
10. `#version 300 es` 依赖 GLES3 上下文；未写最低版本检查与 GLES2 回退（`OpenGlControlBase.GlVersion` 可用）。

## 目标样本

| 启动器 | 路径 | 大小 | 版本 |
|---|---|---|---|
| Plain Craft Launcher 2 | `C:\Users\lenovo\Desktop\Plain Craft Launcher 2.exe` | 5.75 MB | 2.13.1.1 |
| HMCL | `D:\HMCL-3.6.12\HMCL-3.16.3.exe` | 10.9 MB | 外壳 3.7.0.1 / 应用 3.16.4 |
| VersePC | `C:\Users\lenovo\AppData\Local\Programs\VersePC\VersePC.exe` | 223 MB | 1.1.17 |

## 核心结论（速查）

- PCL2：32 位 **VB.NET + WPF（.NET Framework 4.0 CLR）** 单文件 exe；依赖 DLL 全部以 .NET 资源内嵌（Newtonsoft.Json、NAudio、CacheCow、MeloongCore、libwebp64 等）；启动时落盘 `JavaWrapper.jar`(oolloo/jlw)、`libjlw-*.dll`、`Setup.ini`、`LatestLaunch.bat`、`Log1..5.txt`。用 `java -cp ... -jar JavaWrapper.jar <realMainClass>` 规避 Windows 命令行长度限制。
- HMCL：**Java + JavaFX** 应用；exe = `HMCLauncher.exe`(454KB 原生引导器) + 追加 ZIP(10.4MB, 4248 条目)，`Main-Class=org.jackhuang.hmcl.Main`。引导器负责找 Java（HMCL_JAVA_HOME/JAVA_HOME/注册表/bundled jre/下载 JDK）再拉起 JavaFX 主程序；内置 authlib-injector、lwjgl-unsafe-agent、MultiMC bootstrap、Terracotta。
- VersePC：**Electron + 原生 JS 前端 + Node 业务后端**；`resources/app.asar` 133MB(2931 条目)，`main.js` 通过自定义协议 `versepc://` 加载 UI，业务全在 `server.js`(1.1MB)，另有 `agent-engine.js` 多模型 AI Agent、插件系统（modrinth/mod-dev-tools）、monaco/xterm/three.js。Forge 安装复用 `forge-installer.jar`（com.bangbang93.ForgeInstaller，与 PCL2 内嵌的同一个类）。
- 签名现状（2026-10-24 用 `Get-AuthenticodeSignature` 复核）：三个样本 exe 全部 `NotSigned`；HMCL 另有**应用层**更新签名公钥 `assets/hmcl_signature_publickey.der`，PCL2 内置「Microsoft Defender 添加排除项」帮助文档——即行业普遍用"运行时引导 + 自身更新校验"替代 Authenticode。
- PCL2 许可证（2026-10-24 核实 `Meloong-Git/PCL`）：**不是 MIT/Apache**，而是《PCL 分发有限许可》+《存储库合理使用指南》。"重度使用"（基于仓库修改，或参考大量内容重制启动/下载/Mod 管理等实质功能）要求：名称以 `Plain Craft Launcher (PCL)` 开头、关于页首位署名并附赞助链接、公开源码并沿用其指南、实现正版/赞助劝导、Windows 主色相必须为蓝色。**与本项目 MIT + `Lumen` 不兼容，结论：不 fork**。

## 取证产物（仅本地，**不入库、不作为交付物**）

- `_probe/*.py`：PE / 字符串 / asar / zip 解析脚本
- `_probe/versepc/`：VersePC 主进程、后端、预加载、插件清单、AI 配置等抽取文件
- `_probe/hmcl/`：HMCLauncher.exe、hmcl.properties、boot.properties、openjfx 清单等
- 真实启动命令样本：`C:\Users\lenovo\Desktop\PCL\LatestLaunch.bat`

## 协作进度日志（唯一载体）

```
[2026-10-24] 调研 + 技术规划 + technoledge v0.3 完成。
             决策：代号 Lumen；A=核心 / B=UI 保持；GitHub 私有 + SSH 公钥；不签名仅 SHA-256；MIT 许可；
                   Forge ≤1.12.2 用 bangbang93（许可证为发布门禁）。
             下一步：M0 骨架（按用户要求暂不搭建）。

[2026-10-24] 已生成开发用 SSH 密钥（ed25519，无口令）：
             私钥 C:\Users\lenovo\.ssh\lumen_ed25519（未入库、未输出到对话）
             公钥指纹 SHA256:FxVW/gbMficPPOxpE+7o6QhqXhXBTY90st1ATUODmGM
             待朋友把公钥加到 GitHub（个人 SSH key 或仓库 Deploy key，二选一）。

[2026-10-24] SSH 已配置：`~/.ssh/config` 新增 `Host github.com`（IdentityFile=lumen_ed25519，
             IdentitiesOnly=yes），原配置备份为 `config.bak-20261024`。
             已验证：握手已提供该公钥；当前 GitHub 返回 `Permission denied (publickey)`，
             属预期——公钥尚未加入账号/仓库。

[2026-10-24] technoledge 升至 v0.4：
             数据目录拆 Roaming/Local；argfile 等首行输出后删 + 按 JVM 编码写；
             完整性改为"快校验 + mtime 缓存"；门面补 StopGame/GetTasks/GetInstance/SetActiveAccount 等；
             CF 决策写入 §6.3.1（默认 C，待拍板）。仍未开始搭建。

[2026-10-24] 仓库连接实测通过：
             SSH 认证返回 `Hi limpidus-xiaoche/Lumen!`（说明公钥以 Deploy key 绑定到该仓库）；
             `git ls-remote` 退出码 0 且无 ref → 仓库可读但为空；
             本地 `d:\启动器` 尚未 git init。未执行任何提交或推送。

[2026-10-24] 仓库已接通（写权限验证通过）：
             本地已 git init（main），首个提交 c2d2f65（README/technoledge/.gitignore/.gitattributes）；
             远程 origin = git@github.com:limpidus-xiaoche/Lumen.git，main 已推送并跟踪；
              `_probe/` 已加入 .gitignore，不提交；提交身份为仓库级 ttss4096 <ttss4096@outlook.com>（未改全局）。

[2026-10-24] 许可证核实（PCL2）：仓库采用《PCL 分发有限许可》+《合理使用指南》，非 MIT/Apache；
             重度使用条款（PCL 开头命名/蓝色主色/署名+赞助/沿用指南）与本项目 MIT+Lumen 冲突；
             结论：不 fork、不复制代码与资源；改为评估 CmlLib.Core（MIT）复用元数据/下载/启动原语。

[2026-10-24] 评估 XMCL（X Minecraft Launcher，voxelum，MIT，Electron + TypeScript）：
             许可证允许 fork/改名/闭源分发，只需保留版权与许可声明；
             其 @xmcl/core、installer、user、curseforge、modrinth、file-transfer 等包覆盖启动器核心；
             但与 Lumen 的 .NET/Avalonia 栈不兼容——直接复用需整体转 Node/Electron，或增加 Node sidecar。
             结论（已拍板）：**放弃 fork XMCL，维持路线 C**（.NET + CmlLib.Core）；XMCL 仅作 UX 参考。

[2026-10-24] technoledge v0.5 回写完成：
             CmlLib 能力边界表 + M0 门禁；协作身份记录为**临时 deploy key**（目标仍是 collaborators + 用户级 SSH）；
             PCL2 许可原文存档 docs/legal/；argfile 改 CodePages + 静态决定 + 缓存；
             移除 MCBBS、源表改 sources.json；补 Cancel/Retry/GetRunningSessions 与全部 DTO；
             下载 URL 白名单/私网拒绝；java-runtime 只取 raw；发布形态/R2R、integrity CI 生成、路径校验与 blobs GC。
             仓库协作收尾待 owner 操作（加 collaborators、降权 deploy key）。

[2026-10-24] M0 完成：
             solution + Launcher.Protocol / Core / App / Cli；SQLite v3 + 迁移入口；离线 UUID/伪 token；
             六页 Avalonia 壳 + FakeLauncherFacade；Serilog；单实例互斥；LICENSE / THIRD_PARTY_NOTICES / 许可存档；
             build 0 警告 0 错误；8 项测试通过；CLI 与 GUI 冒烟通过；pre-push hook 文件已提交。

[2026-10-24] 协作方式临时调整：`ttss4096` 账号暂不可用，继续使用仓库 deploy key 推送；
             工作流改为「推 feat/* 分支 → owner 在网页端合并」，不直推 main；
             账号恢复后再迁移到 collaborators + 用户级 SSH，deploy key 降为 CI 只读。

[2026-10-24] M0 App 归属确认：
             A 代搭 `src/Launcher.App` 与 `tests/Launcher.App.Tests` 的最小脚手架（六页 + Fake + 深色主题）；
             B 接管，可整体重写；自 B 首次提交起，App 路径只由 B 修改，A 不再改动。

[2026-10-24] 前端策略更新：App 归属仍为 B；后续由 A 提供**最小基础模型**（页面骨架/绑定/数据接口），
             B 负责优化与设计定稿；A 只在 B 明确请求时修改 App。

[2026-10-24] M1（A）完成：
             ManifestClient（官方 + BMCLAPI fallback + 15 分钟缓存，正确解析 latest.release/snapshot）；
             VersionResolver（inheritsFrom 递归合并、库按 group:artifact 去重、参数拼接）；
             RuleEvaluator（os.name/arch/version/features）；JavaLocator（JAVA_HOME/注册表 32+64/常见目录/PATH/内置 runtime）；
             JavaVersionParser（release 优先、属性回退）；CLI 增加 versions / java / resolve；26 项测试通过。

[2026-10-24] M1 前端基础模型：
             App 改用真实 LauncherFacade（CoreServices 组装路径/设置/SQLite/manifest/Java）；
             下载页加载 manifest 并列出真实版本；设置页加载并显示本机 Java；
             新增 2 项 ViewModel 测试（总计 28 项）；视觉与交互待 B 优化。

[2026-10-24] M2（A）完成：
             DownloadEngine（16MB 阈值分片、8MB/片、Range 续传、顺序 SHA1、镜像回退、URL 安全校验、逐跳重定向）；
             VanillaInstaller（client jar/libraries/natives/assets、legacy virtual、磁盘预检、install journal、状态机）；
             TaskRegistry + ThrottledProgress（10Hz）；Facade install/repair/cancel/retry/getTasks；
             CLI install/repair；35 项测试通过；1.21.1（787.6 MB 资源）与 1.12.2 完整安装、秒装、修复均实测通过。

[2026-10-24] M3（A）完成：
             ArgumentBuilder（legacy/现代参数、rules、变量展开、未解析 E5002、classpath）；
             JavaVersionPolicy（版本约束 + 1.13+ 64 位过滤）；ArgFileWriter（ANSI/UTF-8、转义、首行后删除、清扫）；
             LaunchService（进程启动、stdout/stderr、退出归因、RunningSessionStore 持久化）；CLI launch/stop/sessions；
             48 项测试通过；1.21.1 真实启动、会话可见、Graceful 停止 exit 0 已实测。

[2026-10-24] 默认游戏语言：
             `GameOptions.EnsureDefaultLanguage` 在安装与启动前检查 `options.txt`，缺失时写入 `lang:zh_cn`；
             已有文件不覆盖（尊重用户改动）；当前实例已修正并在实机启动/退出后验证仍为 zh_cn；测试增至 50 项。

[2026-10-24] M4（A）完成：
             AccountStore（%LOCALAPPDATA%\Lumen\accounts.dat，整文件 DPAPI 加密，accounts[] + active_id 持久化）；
             MicrosoftAuth（Device Code → XBL → XSTS → Minecraft profile；authorization_pending/slow_down/XErr 处理；
             refresh 轮换）；AuthlibAuth（authserver/authenticate，https 强校验）+ AuthlibInjector（1.2.8，SHA1 pin）；
             LaunchService 按激活账户注入凭据（launch --username 可临时覆盖），MSA 剩余 ≤5 分钟自动刷新并回写，
             外置账户强制 -javaagent（注入器缺失 fail-closed）；
             CLI 新增 login / accounts / use / logout / instances，launch 默认使用激活账户；
             ArgumentBuilder 额外 JVM 参数改为列表，支持含空格路径；
             62 项测试通过（Core 57 / App 4 / Integration 1）；实机以离线账户 Tester 启动 1.21.1，日志显示
             "Setting user: Tester"，Graceful 停止 exit 0。微软网页授权与外置服务器登录未做真机验证（需用户凭据）。

[2026-10-24] M4 微软登录真机验证 + 端点修正：
             首次用 `login.microsoftonline.com/consumers/oauth2/v2.0/devicecode` 返回 AADSTS700016——
             `00000000402b5328` 是 Live Connect **v1** 客户端 ID，不能用于 v2；已改为
             `login.live.com/oauth20_connect.srf` + `oauth20_token.srf`，scope `MBI_SSL`，
             XBL `RpsTicket` 改为原始 token（去掉 `d=` 前缀）。
             浏览器实测：Device Code `J37CG3BA` → 微软账号 `tzlleo` 登录成功，账户落盘并设为激活；
             新进程执行 `accounts` 仍可见（重启免登录）；`launch test-1.21.1` 日志 `Setting user: tzlleo`，
             Graceful 停止 exit 0。外置/离线启动不受影响。

[2026-10-24] M5（A）完成：
             LoaderCatalog：Fabric meta（loader/{game}/{loader}/profile/json）、Forge maven-metadata + promotions_slim、
             NeoForge maven-metadata；版本号数值排序与 NeoForge 前缀映射（1.21.1→21.1）。
             LoaderInstaller：Fabric profile 规范化 libraries 后写子版本 JSON 并下载依赖；
             Forge/NeoForge 下载官方 installer（附 .sha1 校验）后执行 `--installClient <shared>`，
             installer 自行处理 processors / bootstraplauncher；非规范版本 ID 自动归一。
             VersionResolver 优先读本地 versions（加载器子版本），LaunchService 按 LoaderIds 解析并复用原版 client jar。
             CLI 新增 `loaders`，`install` 支持 `--loader/--loader-version`；`UpdateInstanceAsync` 支持换加载器。
             测试增至 70 项（新增 LoaderIds / 版本清单解析 / 本地继承合并 / 空 releaseTime 容错）。
             真机：Fabric 1.21.1 + 0.20.1 安装并启动成功；Forge 1.20.1 + 47.4.28 安装并启动成功（MinecraftForge v47.4.28），
             均 Graceful 停止 exit 0。NeoForge：本机 maven.neoforged.net DNS 被污染（解析到 Facebook IP），
             安装器无法下载，代码路径已实现但未真机验收。放弃 bangbang93.ForgeInstaller，统一走官方 installer。

[2026-10-24] M6（A）完成：
             DB `root_path` 成为 gameDir 唯一权威（启动/模组扫描/删除都读它）；外置目录只登记不删除。
             ModScanner：扫描 mods/*.jar 与 *.jar.disabled，读取 fabric.mod.json / mods.toml 元数据，
             启停通过重命名 `.disabled` 实现；CLI mods / mods-toggle。
             ExternalImporter：识别 MultiMC（mmc-pack.json）、HMCL（modpack.json）、PCL/原生（versions）并复制游戏内容，
             排除 libraries/assets/versions/runtime/logs；CLI import。
             删除：`--keep-saves` 先把 saves/screenshots 移到 exports，再删受管实例目录；外部目录与 shared/game 不动。
             测试增至 78 项（新增模组扫描/启停、MultiMC/原生识别与复制、保留存档删除、外部目录保护）。
             真机：模组扫描与启停通过；MultiMC 目录导入成功（未复制 libraries）；delete --keep-saves 导出存档并删除；
             清理了验证用假 mod（该 jar 因 PowerShell 写入 BOM 导致 Fabric 解析失败，与启动器无关）。

[2026-10-24] 选型咨询：`viettranx/3dviz-pro-max` 能否用作前端？
             结论：**不能**。它不是一个前端框架/组件库，而是 Claude Code / Codex 的 agent skill
             （工作流提示词 + Three.js 示例工程 + 素材配方），产物是浏览器里的 Three.js/Blender 场景。
             与 Lumen 的 Avalonia 11 原生桌面栈无交集：接入需嵌 WebView2（Electron 路线），
             会击穿冷启动 ≤1.5s 与内存预算，并破坏「Core 无 UI 依赖 / App 只消费 ILauncherFacade」分层；
             且随附资产含 CC BY-SA（BodyParts3D）与权利未确认项，违反「随包第三方须 MIT 兼容」。
             可用范围仅限：视觉/光照/相机调子的**设计参考**；或将来的**官网落地页**（浏览器环境）。
             若确要 Web 前端，属架构级决策（建议单独评估 Tauri 而非 Electron），不在本轮范围。

[2026-10-24] 首页 3D 效果方案（适配 Avalonia 12.1.4 / .NET 10）：
             已核实：`Avalonia.OpenGL`（含 `OpenGlControlBase`）随主包提供，无需额外包。
             选型：**方案 A = Composition 分层视差（主）**，方案 B = `OpenGlControlBase` 真 3D（可选增强，需降级），
             方案 C = 预渲染序列帧（否决，体积与解码吃冷启动预算，且 Avalonia 无原生动图播放）。
             硬约束：仅 App 层实现（Core 不做 UI 依赖）；首页可见才渲染；失焦/最小化停；GL 初始化延迟到首帧后；
             GL 不可用（RDP/软件渲染/ANGLE 失败）自动降级到 A；新增设置项 `Enable3DHero` 默认开、低配自动关。
             待实测：`CompositionVisual.TransformMatrix` 在 12.1.4 是否公开（决定真透视还是 2D 伪纵深）。

[2026-10-24] 首页 3D 完整方案定稿（宣传导向）：
             视觉概念「维度之门」：5 层视差 diorama + 悬浮启动卡 + 火花/浮尘，青橙对比配色；
             A 层（Composition 视差 + 粒子 + 光晕）为**主体**，常开；
             B 层（OpenGlControlBase 真 3D：旋转方块/星空）为**用户可选项**，默认关，实验性；
             场景状态机 5 态：Idle / Installing / Launching / Running / Error，直接映射 Core 的
             TaskRegistry / InstallProgress / LaunchEvent，实现「功能融入视觉」；
             新增宣传模式：截图模式（固定构图导出 2560×1440）+ 演示脚本 + 隐私脱敏（假账户/假路径）；
             性能闸门：首页可见才渲染、失焦停表、GL 延迟初始化、5 种降级场景不崩；
             待拍板：色板、卡片布局、是否做首启入场动画、B 层默认值、宣传素材是否需要 4:3 安全区。

[2026-10-24] M7（A）完成：
             ModpackInstaller：按文件名识别 Modrinth/HMCL/MultiMC/CF 直链（未知 E3004）；
             Parse 解析 MC 版本 + 加载器（Fabric/Forge/NeoForge）+ 文件清单（跳过 `env.client=unsupported`）；
             InstallAsync 复用 Vanilla/加载器安装 → DownloadEngine 下载（SHA1 校验）→ overrides 复制 → 默认语言；
             CF 包仅导入 overrides（不调 CF API）；无 SHA1 非白名单直链跳过；
             LauncherFacade.InstallModpackAsync + CLI `modpack install`（`--portable` 前置过滤）；
             UrlGuard 白名单补 BMCLAPI；13 项新测试（总计 91）；实机 Modrinth 目录包/.mrpack 导入 + Sodium 校验 + 启动通过。

[2026-10-24] M8（A）完成：
             IntegrityService：`integrity.json` 快校验（入口 exe + 清单，版本/mtime 缓存）/全量逐文件 SHA-256，失败 E8002 fail-closed；
             UpdateService：`releases.win.json` 固定字段解析（未知字段忽略）+ 版本单调（降级需 --force）→ UrlGuard → 下载 `.part`
             → 先 size 后流式 SHA-256（重试 2 次），失败 E8001；`.exe` 静默启动安装；
             DiagnosticsExporter：系统/版本/设置/Java/实例/日志打包，排除 accounts.dat/args.txt/.part.meta，日志 token/Bearer/URL 凭据脱敏；
             LauncherPaths：`--portable` 写探测 + 不可写回退 %LOCALAPPDATA% 并告警；CLI 新增 `integrity/update check|apply/diagnostics`；
             App 首行 `VelopackApp.Build().Run()`；scripts/pack.ps1（publish→integrity→vpk pack→固定清单）+ verify-integrity.ps1 + release.yml；
             实机：快校验 1 文件、全量 52 文件、同长度篡改 → E8002 exit 1、update check → E8001、diagnostics 6 条目；
             打包产物 Setup.exe 68,433,392 B / Portable.zip 60,918,322 B / nupkg，无 MSI；114 项测试通过；0 警告 0 错误。

[2026-10-24] 沉浸式 3D 方案（全屏传送门 + BGM）：
             环境核查：本机存在 Blender **5.2** 配置目录（路径未定位，不在 Program Files）；
             `Avalonia.OpenGL` 随主包可用；**Shell 被 PS 执行策略阻断**，无法运行 blender/npx/dotnet/git。
             Skills 核查：`arjun988/blender-skills`（94 技能，MIT）、`linnnn89/blenderskill`（123 手册）均为真；
             两者只是文档/路由，真正驱动 Blender 还需 `ahujasid/blender-mcp`（MCP 服务）。
             架构结论：Avalonia 无 3D 场景引擎，"所有东西 3D" 拆成 **世界层（OpenGL）+ 面板层（2D 控件透视投影）**；
             若 `CompositionVisual.TransformMatrix` 不可用，则回退「控件 → RenderTargetBitmap → GL 纹理 → 四边形」。
             新增：`src/Launcher.Render`（GL 渲染库）、NAudio+NVorbis 音频、SharpGLTF 加载、射线拾取、
             保留「经典 2D 模式」回退；包体预计 68MB → ~100MB。
             待验证 4 项：3D 变换 API / OpenGL 与 Avalonia 叠加 / Ogg 播放 / Blender headless 导出。

[2026-10-24] S0 验证结果 + 用户拍板：
             环境：Shell 已解禁；Blender `D:\Blender\blender\blender.exe` = **5.2.2 LTS**；dotnet 10.0.300；blender-mcp 已配。
             ✔ **CompositionVisual 暴露 3D TRS**：`Offset/Translation/Scale/CenterPoint: Vector3D`、
               `Orientation: Quaternion`、`RotationAngle` → 控件可直接做 3D 旋转/位移/缩放（路径 A′）。
               未发现显式投影矩阵，透视靠「3D 旋转 + 按深度缩放 + 深度排序」近似，够用。
             ✔ Blender headless 导出：factory-startup 导出 cube.glb（1936 B）成功，管线可用。
             ⚠ Blender 会往安装目录写 `__pycache__`，沙箱只读 → 后续统一用 `--python-expr` 且接受告警，
               或把 PYTHONPYCACHEPREFIX 指向工作区。
             决定：文字一律 **2D 悬浮/弹窗**（GL 内不渲染文字）；
                   每个传送门 = 一个实例；门楣上方悬浮实例图标，悬停显示名称 tooltip（2D 跟随）；
                   保留经典 2D 模式；音乐用**原创 MC 风格环境曲**（MC 原声 C418/Mojang 版权，禁止随包）。

[2026-10-24] `ui.md` 定稿（UI 层唯一实施依据）：末地紫色板 + 统一动效令牌；
              阶段 S1 世界层骨架 → S2 传送门交互 → S3 面板 3D 化 → S4 状态动效 → S5 音频 → S6 硬化与宣传；
              每阶段均含任务清单与**可测量验收标准**（60fps/≤80ms 悬停/≤200ms 进度同步/首帧 ≤1.5s 等）；
              音乐走**自产合成**（tools/audio/gen_ambient.py，FM 电钢 + Schroeder 混响，ffmpeg 转 Ogg）；
              门内为末地紫渐变；附录含降级矩阵、目录清单、A/B 分工与 6 条回退方案。

[2026-10-24] ui.md v1 复审（P0 7 / P1 12 / P2 10，详见上节）：
             关键阻断：ESC 三重语义 + 无边框窗口退出全屏后不可操作；S4 未接 GetTasksAsync/GetRunningSessionsAsync
             导致重启后任务/运行态丢失且无法表达多实例并发；3D 面板命中测试未验证（CompositionVisual 变换
             不参与 Visual 命中测试的风险）；降级矩阵用了 Avalonia 不存在的 SystemParameters.IsRemoteSession；
             UI 专属设置项在 AppSettings/Patch 中不存在，与「Core 不改一行」冲突；包体/内存/CPU 指标基线混用且互斥；
             S3 面板范围缺任务中心/更新/安装向导/首次引导/诊断，与 M2–M8（B）验收不符。
             依赖已核：SharpGLTF.Core 1.0.5（MIT，无 Newtonsoft）、NAudio 3.0.1 + NAudio.Vorbis 3.0.0（MIT，
             需 NAudio.Wasapi 显式引用）、StbImageSharp（Unlicense OR MIT），均需登记 THIRD_PARTY_NOTICES。
             下一步：出 ui.md v1.1（先修 P0 与 §7 引用、图层顺序、状态模型、指标基线），再由 B 落 S1。

[2026-10-24] `ui.md` **v1.1 已回写**（P0 7 / P1 12 / P2 10 全部落位）：
             返回栈统一：ESC = Modal → 面板 → 全屏 → 退出确认；退出全屏恢复 `SystemDecorations=Full` 并钳制位置。
             状态模型：每门一态 `PortalItem.PortalState` + 派生 `ShellMood`；启动时用
             `GetRunningSessionsAsync`/`GetTasksAsync` 恢复，1s 轮询兜底，取消走 `CancelTaskAsync(taskId)`。
             命中测试：**交互控件禁用 CompositionVisual**，一律布局 + `RenderTransform`；门/图标走 OBB 射线拾取；
             `ClipToBounds=false`；S2 第一项就是命中 Spike。
             降级 API 换成真的：`GetSystemMetrics(SM_REMOTESESSION)`、`GlobalMemoryStatusEx`、`LUMEN_DISABLE_GL=1`；
             软件渲染不再承诺帧率。
             设置项：明确扩展 `AppSettings/AppSettingsPatch` 9 个 UI 字段（DTO 变更，业务逻辑不改），旧 JSON 走默认值。
             指标对齐：UI 空闲内存 <150MB（technoledg §6.10）、空闲帧率上限 30fps + CPU <5%、包体基线改 Portable.zip 60.9MB。
             面板补齐 11 个（任务中心/更新/安装向导/导入向导/首启引导/诊断阻断/关于许可）；日志固定走经典 2D。
             其他：图层顺序改为 世界→后处理→面板→Tooltip→Modal；GL 只用 `GlInterface` 不引 Silk.NET（GLSL ES 3.0）；
             上下文丢失/TDR 重建一次再降级；音频显式 `NAudio.Wasapi`，2 首 × 4 分钟；阶段顺序改为
             状态(S3) 先于面板(S4)；S1 执行权归 **B**，A 只交付 `Launcher.Render` + 骨架草案。

[2026-10-24] ui.md v1.1 修订（用户拍板）：
             ① **不脱敏**：`--demo` 与截图模式一律使用真实数据，取消演示数据/`--demo-real`，§14 未决项 5 删除；
             ② **取消 A/B 分工**：§11 改为「维护方式（不分工）」，全文不再有目录归属限制，
                唯一硬约束是 Core 业务逻辑与 ILauncherFacade 契约不被 UI 反向改写；
             ③ **暂不开工**：S1 暂不启动，等后续指令。

[2026-10-24] `ui.md` **v1.2**（回写第二轮复审 P0 4 / P1 12 / P2 10，并修正 v1.1 自身事实错误）：
             **P0-1 渲染归属定死**：门框/门内/图标一律 **GL 渲染**（才有深度/雾/shader），CompositionVisual 只用于无交互装饰；
             键盘与读屏靠透明可聚焦的 `PortalFocusProxy`（Opacity=0、IsHitTestVisible=false、Focusable=true）。
             **P0-2 截图链路改为离屏 FBO + `glReadPixels`**（GlInterface 未暴露则用 GetProcAddress），
             2D 层单独 RTB 后手动 alpha 合成，不再指望 RenderTargetBitmap 抓 GL。
             **P0-3 窗口 API 已核实**：`SystemDecorations` 虽存在但标 `[Obsolete]`，TreatWarningsAsErrors 会构建失败；
             改用 `WindowDecorations`（None/BorderOnly/Full）。
             **P0-4 后端锁定**：`Program.cs` 配 `Win32PlatformOptions.RenderingMode = AngleEgl → Wgl → Software`，
             运行时校验 GLES ≥3.0，Vulkan/Software 直接降级。
             **P1 修正**：`OnOpenGlRender(gl, fb)` 无尺寸参数（用 Bounds×RenderScaling 或 GetPixelSize）；
             丢失用 `OnOpenGlLost()`；命名空间 `Avalonia.OpenGL.Controls`；`Launcher.Render` 改依赖自有 `IGlApi`；
             投影统一 DIP + `RenderTransformOrigin=TopLeft`；面板取消倾斜验收；看门狗仅可见期计时；
             启动恢复改读 DB state，深度校验延后；首帧关键路径、Fake 不静默回退、音频设备失败兜底、焦点作用域均已写死。
             **P2**：technoledg 拼写全改、诊断/关于改设置内可见入口、AccentColor 单一来源、删 E9001、CI 加 glslangValidator。

[2026-10-24] **S1 世界层骨架 已完成（首个可跑版本）**：
             新增 `src/Launcher.Render`（已加入 Launcher.slnx，不依赖 Avalonia）：
             `IGlApi` + 自有 GL 函数指针加载、`Vec3/Mat4`、`Camera`、`PortalLayout`（纯函数 + opacity clamp）、
             `SceneRenderer`（天穹渐变、悬浮岛、门框、门内叠加发光、距离雾、GLES 3.0 版本闸门）。
             App：
             `Program.cs` 锁定 `Win32PlatformOptions`（AngleEgl→Wgl→Software，WinUIComposition→RedirectionSurface，PerMonitorDpiAware）；
             `World/WorldView`（OpenGlControlBase 宿主，含 GlApiAdapter、60fps 节拍、失焦降 5fps、OnOpenGlLost 重建、首帧/帧时间埋点）；
             `Shell/ImmersiveShell`（世界层 + 顶部导航 + 右侧经典页面浮层 + 降级横幅）；
             `Shell/CameraController`（8s 公转 ±3° + 鼠标视差 ±2°）；`Shell/InputRouter`（ESC 栈 + F11，退出全屏恢复 WindowDecorations=Full 并钳制位置）；
             `App.axaml.cs` 取消静默回退 Fake（仅 `--fake` 允许），Core 失败改为可见错误窗口。
             **实测**：GL 后端 = `OpenGL ES 3.0 (ANGLE 2.1.1)` / Intel Arc 140T；真实 4 个实例 → 4 扇门；
             `LUMEN_DISABLE_GL=1` 触发降级横幅且不崩；Debug/Release 均 0 警告 0 错误。
             **未达标项**：首帧实测 5.2–8.1s（开发态、沙箱内、未 R2R），需在 R2R publish + 冷/热缓存条件下重测；
             帧时间采样需窗口聚焦（失焦只跑 5fps，样本不足 120 帧不输出）。
             下一步：S2 命中测试 Spike + 图标 billboard + Tooltip。

[2026-10-24] 视觉整改 R1（末地风 + 下界门 + 原版方块资产）：
             风格：天穹改末地虚空（近黑 + 紫灰雾带 + 程序化星点）；岛改末地石 + 紫珀镶边；远景加 5 根黑曜石柱；
             门改为黑曜石框 + 多倍频漩涡 shader（极角随半径旋转）+ 地面低强度紫色光晕。
             布局：门改为**以主门为中心左右交替展开**（0,+1,-1,+2,-2…），不再单侧堆叠。
             资产管线（新）：`StbImageSharp` + `BlockModelLoader`（解析原版/Blockbench models，支持 parent 合并、UV、六面）；
             `MinecraftAssetProvider` 从**用户本机** `versions/<ver>/<ver>.jar` 读 `end_stone`/`obsidian`/`purpur_block`
             模型与贴图（不随包分发 Mojang 资产）；渲染器补纹理采样、UV 顶点属性、NEAREST 采样与图集外单贴图绑定；
             找不到客户端 jar 时自动回退程序化配色。
             修复：导航列表补 ItemTemplate（不再显示 `NavItem{...}` 的 ToString）；选中项改紫色；右侧浮层 640→470。
             实测：`shot8.png`（_probe，不入库）显示末地石贴图、紫珀镶边、三门对称、漩涡正常；构建 0 警告 0 错误。
             **仍待改**：岛只有 1 层厚、边缘规则；黑曜石框因贴图偏暗仍像平板；门底光斑仍偏亮；
             第 4 扇门被右侧浮层遮挡；贴图整体偏亮（shader 里 `*1.6` 需要下调）。
             下一步：岛加厚到 2–3 层并做不规则边缘、门框拉开块缝、贴图亮度校正、相机再抬高一点。

[2026-10-24] 视觉整改 R2（真纹理打通 + 构图重排）：
             修掉 4 个真 bug：
             ① 原版模型 `block/cube_all` 的父级贴图写成 `#all`，直取 `Textures.Values.First()` 会拿到 `#all` →
                新增 `BlockModelLoader.ResolveTexture/PickPrimaryTexture` 递归解引用；
             ② shader 里 `mix(vColor, tint, uTextured)` 把顶点色整个丢掉，导致贴图压不暗 →
                改为 `vColor * tint`；
             ③ 门内用加色混合，把过亮地面透出来形成白斑 → 改标准 alpha 混合；
             ④ jar 选择误取整合包模组 jar → 只认 `versions/<ver>/<ver>.jar` 且必须含 `models/block/end_stone.json`。
             视觉：门框改为 0.45 块的黑曜石方块（2 柱 × 4 + 顶 2），内框与 UV 同步缩放；
             岛缩为 7×6、两层、边缘随机缺块、顶点色压到 0.62；相机 12.6 / 3.15 / 目标 (0,1.15,-0.55)；
             门半径 3.2、步进 30°；进度条改紫色（App.axaml style）。
             实测：`shot18.png`（_probe）—— 原版末地石与黑曜石贴图、四门不重叠、完整岛体。
             仍丑的点：门相对岛偏小、岛前缘有缺口、氛围光源与粒子全缺。
             开源 UI 调研（本轮）：SukiUI(MIT, 6.x 针对 Avalonia 11.3.11)、Semi.Avalonia(MIT)、
             Material.Avalonia(MIT)、FluentAvalonia(MIT)；XMCL(MIT, Vue/Vuetify) 可作交互参考，
             Prism(GPL)/HMCL(GPL) 仅可参考不可复制代码。结论：暂不引入重型主题，先自建令牌。

[2026-10-24] ui.md **v1.1 第二轮复审**（P0 4 / P1 12 / P2 10，详见上节）：
             已确认 v1 的 7 项 P0 全部回写；本轮用 Avalonia 12.1.4 源码复核后仍残留：
             ① 门由 GL 还是 CompositionVisual 渲染，§3.2 与 S1 任务/§8.2 shader 自相矛盾；
             ② 截图：OpenGlControlBase 把画面挂成 composition child visual，RenderTargetBitmap 抓不到，
                2560×1440 导出必须走 glReadPixels / 屏幕捕获；
             ③ §1.1 仍用 SystemDecorations（Avalonia 12 文档属性为 WindowDecorations，WindowState 已 direct）；
             ④ 未在 Program.cs 锁定 Win32 渲染后端，Vulkan/Software 下世界层必失败。
             P1 关键：Launcher.Render「不引 Avalonia」与只许用 GlInterface 冲突；OnOpenGlRender 无 w/h、
             ctx.IsLost 不可访问；§3.3 物理像素/DIP 混用 + RenderTransformOrigin 默认中心；仿射面板却验收“倾斜 15°”；
             1s 无帧误判上下文丢失（与失焦停渲染冲突）；GetInstanceAsync 不做目录校验却用于 Broken 判定；
             相机默认值仍缺；首帧关键路径未定义；Core 失败静默回退 Fake；音频设备失败无兜底；
             快捷键作用域只覆盖 ESC；取消 A/B 与 README/technoledge 旧分工冲突。
             下一步：先修 4 项 P0（门渲染层二选一、截图方案、WindowDecorations、后端锁定）再进 S1。
```

## 待办（下一步）

- **推送已恢复（2026-10-24）**：deploy key 无 workflow 权限，导致含 `.github/workflows/release.yml` 的提交被拒；
  已生成用户级 ed25519 key（`~/.ssh/lumen_user_ed25519`）并加入账号 `mc-ttss4096`，owner 已加为 Write 协作者，
  `feat/m8-core` 推送成功（SSH 身份 `Hi mc-ttss4096!`）。deploy key 保留作为后备，待账号稳定后降为 CI 只读。
- 发布流程：先打一个旧版本 tag 走 `release.yml`，再做「旧版 → 新版」端到端升级实测（当前只验证了打包与校验链路）。
- B 接管 `src/Launcher.App`：M2–M8 的 UI 部分（任务中心、实例卡片、账户页、安装向导、导入向导、更新入口、首次启动引导、日志轮转设置）。
- 协作收尾：`ttss4096` 账号恢复后，把 deploy key 降为 CI 只读；当前推送身份为 `mc-ttss4096`（用户级 SSH key）。

## 附：合并前 origin/main README 原文（2026-10-25 归档）

# MC 启动器原理调研（本地取证）

> 本文件按用户规则实时记录进度。原理结论主要来自本机静态分析；PCL2/XMCL 许可证与仓库信息来自官方链接（原文已存档 `docs/legal/`）。

## 进度

- [x] 三个目标的文件/版本信息
- [x] PE 结构解析（`_probe/pe_probe.py`）
- [x] 字符串特征扫描（`_probe/str_probe.py`）
- [x] PCL2：.NET 程序集反射、内嵌资源清单、运行时目录、真实启动命令行
- [x] HMCL：自解压 ZIP 结构、内嵌 jar 清单、HMCLauncher 引导器分析
- [x] VersePC：app.asar 解析、主进程/后端代码抽取、插件与 AI 模块
- [x] 汇总对比与实现建议（见对话中的调研报告）
- [x] 完整技术结构规划方案（技术栈、分层架构、核心子系统算法、里程碑、验收标准）
- [x] 并行开发协作规范 `technoledge.md`（契约 v0.1、目录归属、PR 流程、M0–M8 分工）
- [x] `technoledge.md` v0.1 审查（2026-10-24）：P0 8 项 / P1 8 项 / P2 12 项，详见下方「文档审查记录」
- [x] `technoledge.md` 升版 **v0.2** 并回写：P0/P1/P2 全部落位（逐项对照见文档 §17 评审响应表）
- [x] 补充 §12.1 完整性校验（v0.3 定稿为 SHA-256 + fail-closed；曾拟公钥签名，已按决策取消）
- [x] **决策定稿 v0.3**：代号 `Lumen`；A=核心 / B=UI 保持；GitHub 私有 + SSH 公钥；**不签名，仅 SHA-256 哈希校验**；本项目 **MIT**；Forge ≤1.12.2 使用 `bangbang93.ForgeInstaller`（许可证为发布门禁）
- [x] `technoledge.md` **v0.4**：数据目录拆分（Roaming/Local）、argfile 竞态与编码、完整性校验时机、门面补口、CurseForge 决策项
- [x] CurseForge 方案拍板：**C**（Modrinth/HMCL/MultiMC；CF 仅直链包，M7 不验收 CF API）
- [x] GitHub 账号等级确认：**Free**（分支保护降级为约定 + pre-push hook + CI，CODEOWNERS 仅作 review 路由）
- [x] XMCL 评估（MIT，可 fork）：**放弃**；维持路线 C（.NET 10 + Avalonia + CmlLib.Core），XMCL 仅作 UX 参考
- [x] `technoledge.md` **v0.3 复审**（2026-10-24）：新增 P0 5 项 / P1 16 项 / P2 11 项，见下「v0.3 复审记录」
- [x] `technoledge.md` 升版 **v0.4** 完成：P0-1..P0-5 全部落位（CF=C、GitHub=Free）
- [x] `technoledge.md` **v0.4 复审（两轮）**：第 1 轮 6 个 P0 + 第 2 轮 3 个新增 P0，见下「v0.4 复审记录」
- [x] `technoledge.md` 升版 **v0.5**：P0/P1 全部回写（CmlLib 边界、协作身份、许可存证、编码/源/任务/SSRF/lzma、DTO、孤儿进程、hook、发布形态、路径/GC）
- [ ] **临时**：使用仓库 deploy key 推送（`ttss4096` 账号暂不可用）；账号恢复后迁移到 collaborators + 用户级 SSH，deploy key 降为只读
- [x] **M0 完成**：解决方案 + Protocol/Core/App/CLI + SQLite v3 + 离线身份 + 六页 Avalonia 壳 + FakeLauncherFacade + 8 项测试 + pre-push hook 文件
  - **归属说明**：`src/Launcher.App/**` 与 `tests/Launcher.App.Tests/**` 由 **A 代搭最小脚手架**（六页 + Fake + 深色主题），由 **B 接管**；B 首次提交后 App 路径只由 B 修改。
- [ ] B 接管 App：在 `feat/ui-m0` 分支确认或重写脚手架，并在提交信息中说明
- [x] **M1（A）完成**：manifest（含 `latest` 解析）、version.json、rules、`inheritsFrom` 合并、Java 探测（32/64 注册表）
- [x] **M1（B）基础模型**：下载页接真实 manifest 列表、设置页接真实 Java 列表；B 负责优化定稿
- [x] **M2（A）完成**：下载引擎（分片/续传/SHA1/URL 安全）、Vanilla 安装、install journal、秒装、修复、CLI install/repair
- [x] **M3（A）完成**：参数拼装/变量展开、Java 选择、argfile 编码与生命周期、进程启动/日志、退出归因、运行会话、CLI launch/stop/sessions
- [x] **M2/M3 前端接线（B）**：下载页「原版游戏」版本筛选/搜索 + 安装进度条；加载器版本号列表（Fabric / Quilt / Forge / NeoForge）+ Fabric / Quilt 一键安装；Home 账号区移至启动按钮上方
- [x] **M2（B）任务中心**：左侧底部「任务」入口；任务列表（安装 / 修复 / 整合包）、状态胶囊、进度条（`Percent=-1` 走不确定态）、取消 / 重试 / 修复按钮；Core 任务落库并在启动时恢复，中断任务标记为可修复
- [ ] **M3（B）运行体验**：实例卡片、实时日志面板、历史日志（未做）

## v0.4 复审记录（已回写 v0.5，保留追溯）

> 处理结果见 `technoledge.md` §17 的 v0.5 各行与 §13-21/22/23。

第 1 轮（已回写 v0.5）：

1. §6.5 L404 兜底「JDK ≤17 用 `Encoding.Default`（CP936）」在 .NET 5+ 不成立（`Encoding.Default` 恒为 UTF-8）→ 需 `CodePagesEncodingProvider` + `GetEncoding(ANSICodePage)`，并加包 `System.Text.Encoding.CodePages`
2. §6.5 L402 为读 `sun.jnu.encoding` 每次启动多起一个 JVM（100–300ms）→ 改按 JRE 大版本静态决定 + 按 java 路径缓存
3. §6.2 L343 `download.mcbbs.net`：MCBBS 已于 2024 年关站，需实测；源表应改为可下发的 `sources.json` 而非 Core 常量
4. §5 缺 `CancelTaskAsync(taskId)` / `RetryTaskAsync(taskId)`：`GetTasksAsync` 恢复的任务无 CT 持有者，实际无法取消
5. 整合包清单里的下载 URL 无约束 → SSRF/任意拉取；需仅 https + host 白名单 + 禁 localhost/`file:`
6. §6.4 L396 JRE 的 `lzma` 变体未提：System.IO.Compression 不支持 LZMA → 明确只取 `raw` 或引入 7z（含许可证）

第 2 轮（本轮新增内容：CmlLib.Core / PCL2 许可边界 / XMCL 否决 / 真实仓库）：

7. **CmlLib.Core 与自研内核的能力边界未定**：它自带下载/安装/启动/Java 逻辑，直接复用会绕过 §6.2 的 `.part.meta`、镜像健康度与 §6.3 的 install journal（M2「中断可继续」失效），也可能自行拼命令行（M3 argfile/wrapper 失效）→ 需能力边界表：只复用元数据解析与 natives/asset 提取；下载、参数拼装、进程管理必须留在 `Launcher.Core`
8. **Deploy key 无法支撑双人 PR 流程**：deploy key 是仓库级凭据，无法代表两个 GitHub 用户 → PR 作者/approve/CODEOWNERS 全部失义；Free 计划无分支保护 + 写权限 key = 可直推/强推 `main` → 改用两个协作者账号
9. **PCL2 许可证结论是转述**：§12 的「名称 PCL 开头 / 蓝色主色相 / 关于页署名」属法律判断，需附许可原文链接 + 存档日期 + 本地副本，否则不可核验

第 1 轮 P1（未回写）：DTO 字段全缺（`LauncherSnapshot/InstanceSummary/InstanceDetail/AccountSummary/ModEntry/ModPackInstallRequest/AppSettingsPatch`）；`StopGameAsync(instanceId)` 无法处理孤儿进程（缺 `GetRunningSessionsAsync`）；§9.0 pre-push hook 无安装步骤（§15 缺 `core.hooksPath`）；authlib-injector SHA1 无基准（需内置 pin 表）；`integrity.json` 未写 CI 构建时生成；发布形态（self-contained/R2R/单文件/裁剪）未定；外部导入「复制 vs 原地接管」未定；路径安全校验缺失；`blobs` 无 GC；§10 M0 仍写「SQLite v2 / 契约 v0.2」；§1 仍列 CurseForge；§6.4「v1 只接受 x64」措辞冲突；§11 矩阵含非法组合。

## v0.3 复审记录（已回写 v0.4，保留追溯）

> 处理结果见 `technoledge.md` §17 的 v0.4 各行；以下为原始复审条目。

P0（会造成实际故障或阻断里程碑）：

1. `%APPDATA%\Lumen` 存放数 GB 的 versions/libraries/assets → 域环境漫游配置会同步爆炸，应改 `%LOCALAPPDATA%`
2. argfile「`Process.Start` 返回后立即删除」存在竞态（JVM 可能尚未读取即被删）→ 改为观察到首行输出后删除 + 失败重试 + 启动时清扫
3. argfile 编码必须用系统 ANSI（中文 Windows = GBK）：Java 9–17 不按 UTF-8 解析该文件，否则中文/空格路径乱码，M3「中文路径」验收必挂
4. §12.1 每次启动全量 SHA-256 校验自身文件 vs §6.10「冷启动 ≤1.5s」冲突（self-contained 约 100MB+）→ 改为「版本变更后校验」或关键文件子集 + 校验记录
5. CurseForge 需 API key 才能解析整合包 `projectID/fileID`，文档未定义来源与配置策略 → M7 硬阻塞

P1：门面仍缺 `StopGame/GetInstanceAsync/GetTasksAsync`、`ImportExternalAsync` 无进度、`SetActiveAccount`、`LoginRequest` 无 Offline 模式；Progress 回调线程与事件节流未定义；镜像源列表与优先级未定义（B 的「下载源」无数据来源）；分片多源混下无法归因校验失败；「秒装」全量 SHA1 与 `blobs.verified_at` 快路径矛盾；1.13+ 必须 64 位 JRE 未写；内存/JVM 默认策略缺失；Forge ≤1.12 回退官方 installer 路径未验证；Quilt 范围不一致；Protocol 目录缺 `Loaders/Settings`；`NuGet.config` 国内镜像与 GitHub CI 冲突；DB 备份前需 `wal_checkpoint`；`download_queue` 缺 `instance_id/task_id/attempts`；日志 sink 与 CLI 解析器选型未定；私有仓分支保护/CODEOWNERS 强制需确认计划；§17 残留「云 HSM」与 v0.3「不签名」矛盾，版本号标记混乱（标题 v0.3 / 各节 v0.2 / 契约表缺 v0.3）。

P2：CF/HMCL/MultiMC 包内文件名未明确、依赖补齐能力被高估、`E5003` 内部兜底不应作错误码、`0xC0000005` 为负 int 需按无符号比较、崩溃报告不入日志轮转、便携模式写权限回退、`None` 隔离 gameDir 未写死、`instance.json` 与 DB 权威未定义、升级 mods 兼容提示、测试缺项（代理/E2004/便携/换机 DPAPI/单实例锁/DTO 源生成覆盖/3000 asset 压力）、错误码 i18n key、第三方表缺「是否随包」列。

## 文档审查记录（technoledge.md v0.1 → v0.2，已全部处理）

> 处理结果逐项对照见 `technoledge.md` §17；以下保留原始审查条目用于追溯。

P0（阻塞并行开发，必须先改文档）：

1. 跨平台宣称 vs Windows-only 实现（DPAPI / 注册表 / MSI / Authenticode / @argfile）→ 明确 v1 仅 Windows，或抽象 `ICredentialProtector`
2. `ILauncherFacade` 缺 7 类能力（模组启停、实例编辑/隔离、加载器版本列表、整合包安装、设置写入、更新检查、历史日志）→ M5–M8 无法按文档实现
3. `@argfile` 把 accessToken 明文落盘 → 违反「禁止明文落盘」；诊断包需排除
4. `Launcher.Core` 禁用容器 vs `Microsoft.Extensions.Http.Resilience`（DI 中心）→ 改用 Polly `ResiliencePipeline`
5. 分发矛盾：Velopack（Setup.exe 增量）vs MSI；签名证书 2023 起需 HSM/云签名
6. 里程碑依赖倒置：M3 验收要离线登录，但离线认证在 M4
7. 分片下载策略与真实网络行为冲突（4MB 分片对小 asset、SHA1 不可并行分段、429 降权、缺 `.part` 元数据）
8. authlib-injector（AGPL-3.0，需复核）+ Minecraft EULA 合规风险 → 改运行时下载 + 合规声明

P1（一致性）：`${...}` 断言与 legacy 参数冲突、Protocol 规则与门面位置矛盾、进度日志双源（README vs §14）、.NET 8/10「API 兼容」说法、Forge 1.13–1.16.5 安装路径缺失、测试矩阵缺 Java 16、CLI `--offline/--dir` 语义不一致、`tests/Core.Tests` 归属矛盾。

P2（工程细节）：DB 缺字段/迁移、无安装事务与实例状态机、缺代理/超时/重试/磁盘预检/单实例锁/队列持久化、缺 `.editorconfig`/`global.json`/`NuGet.config`/`.config/dotnet-tools.json`、非 ASCII 路径与 longpaths、注册表 32/64 视图、隔离语义与 natives 归属、删除实例保留存档、命名与 VersePC 撞车、性能预算/日志轮转/错误码、第三方组件表缺项、`file_cache` 内容寻址设计、`Percent` 语义（未知总量）。

## 规划定稿（速查）

- 代号：`Lumen`（发布名待定，避开 VersePC 前缀；发布前做商标检索）。
- 平台：**v1 仅 Windows 10/11 x64**；跨平台列入 v2 非目标（DPAPI / 注册表 / 签名均为平台相关）。
- 技术栈：`.NET 10 LTS`（环境不一致则统一 `.NET 8 LTS`，单一 TFM 由 `global.json` 钉死）+ `Avalonia 12.1.4` + `CommunityToolkit.Mvvm` + `Polly.Core`；`System.Text.Json` 源生成；SQLite 存索引，JSON 存配置。
- 形态：单进程 GUI + 可复用 `Launcher.Core` 类库 + `Launcher.Cli`（内核可被 CLI/测试直接驱动）；`Core` 不引用任何 UI 类型。
- 规避 32K 命令行：Java 9+ 用 `@argfile`（**等首行 stdout 后删除** + ACL 收紧）；Java 8 回退 wrapper jar（对标 PCL2 JavaWrapper）。
- Forge/NeoForge：≤1.12.2 使用 `com.bangbang93.ForgeInstaller`（**许可证核验为发布门禁**），1.13+ 走官方 installer；`authlib-injector` 不随包分发。分发只用 Velopack（`Setup.exe` + delta + Portable zip），不产 MSI。
- 完整性：**不签名**（无 Authenticode、无应用层公钥签名），只用 **SHA-256 清单**（自身 `integrity.json` + 更新清单）并 fail-closed；安全边界与残余风险见 technoledge §12.1。
- 仓库：**GitHub 私有**，两位开发者各自 SSH 公钥（ed25519），`main` 保护 + 对方 1 approve，CODEOWNERS 按 A/B 目录划分。
- 复用策略：优先评估 **CmlLib.Core（MIT）** 覆盖元数据/下载/启动原语；**不 fork XMCL / PCL2**，XMCL 仅作 UX 参考。
- 许可证：本项目 **MIT**；随包第三方必须与 MIT 兼容，不兼容者仅运行期下载/调用，并登记 `THIRD_PARTY_NOTICES.md`。
- v1 范围：元数据 → 安装 → 启动 → Java 管理 → 离线/微软/外置登录 → 实例隔离 → 模组/整合包 → 自更新。AI/联机/皮肤站作为 v2。

## M0 验收结果（2026-10-24）

| 项 | 结果 |
|---|---|
| `dotnet build` | 0 警告 / 0 错误（SDK 10.0.300，`net10.0-windows`） |
| `dotnet test` | 8 项通过（Core 5 / App 2 / Integration 1） |
| CLI `offline-uuid --name Player` | `a01e3843-e521-3998-958a-f459800e4d11` |
| CLI `paths` | `%APPDATA%\Lumen` / `%LOCALAPPDATA%\Lumen` 已拆分 |
| GUI 冒烟 | `Lumen.exe` 启动并保持运行；日志写入 `%LOCALAPPDATA%\Lumen\logs` |
| 架构测试 | Core 不依赖 Avalonia / Hosting / DI |
| pre-push hook | 文件已提交，待执行 `git config core.hooksPath .githooks` 启用 |
| App 归属 | A 代搭脚手架；B 接管；后续基础模型由 A 提供、B 优化 |

## M1 验收结果（A 部分，2026-10-24）

| 项 | 结果 |
|---|---|
| `versions --limit 5` | `latest_release=26.3` / `latest_snapshot=26.4-snapshot-5`，清单解析正确 |
| `resolve 1.21.1` | mainClass `net.minecraft.client.main.Main`；97 个库；28 game args；11 jvm args；assetIndex 17 |
| `java` | 检出 6 个 JDK（Oracle 25/21、Temurin 21/17、Microsoft 21，全部 x64） |
| 测试 | **28 项通过**（Core 23 / App 4 / Integration 1） |
| 构建 | 0 警告 / 0 错误 |
| 前端基础模型 | 下载页接 `RefreshManifestAsync`（真实版本列表）；设置页接 `GetSnapshotAsync`（Java 列表）；App 启动使用真实 `LauncherFacade` |
| GUI 冒烟（真实 Core） | `Lumen.exe` 启动，SQLite 初始化成功，日志无错误 |

> B 部分只做了基础模型（数据绑定 + 列表），视觉与交互仍由 B 优化定稿。

## M2 验收结果（A 部分，2026-10-24）

| 项 | 结果 |
|---|---|
| `install 1.21.1`（完整资源） | 成功；70 个库；**787.6 MB** 资源；客户端 jar SHA1 与 manifest 一致 |
| `install 1.12.2`（完整资源） | 成功；1290 个 asset、105 个库文件、11 个 native；客户端 SHA1 一致 |
| 秒装 | 重复安装 1.12.2（含资源）**1.61s**；1.21.1（跳过资源）**1.14s** |
| 修复 | 删除 `patchy-1.3.9.jar` 后 `repair test-1.12.2`，**3.78s** 恢复 |
| 测试 | **35 项通过**（Core 30 / App 4 / Integration 1） |
| 构建 | 0 警告 / 0 错误 |
| 安全 | URL 校验：仅 https、host 白名单、私网/环回拒绝、重定向逐跳复检；单测覆盖 |

> B 部分任务中心已完成（见下一节）；Core 提供 `TaskRegistry`，本轮补上了 SQLite 任务落库与重启恢复。
> 断点续传逻辑已实现（`.part` + `.part.meta` + 分片文件），但本轮未做"中途强杀后续传"的实测。

## M3 验收结果（A 部分，2026-10-24）

| 项 | 结果 |
|---|---|
| 真实启动 | `launch test-1.21.1 --username Player` 成功；游戏进入主菜单（日志出现 atlas 创建） |
| 运行会话 | `sessions` 显示 `pid=83128`；`stop` 后输出"没有运行中的游戏" |
| 停止 | `stop` 使用 Graceful，游戏打印 `Stopping!`，退出码 **0** |
| 日志 | stdout/stderr 实时回传；退出归因已接（崩溃报告/退出码） |
| 测试 | **50 项通过**（Core 45 / App 4 / Integration 1） |
| 构建 | 0 警告 / 0 错误 |
| 默认语言 | 安装/首次启动前写入 `options.txt` `lang:zh_cn`；实机启动并退出后仍为 `zh_cn` |
| 已知 | 1.21.1 命令行未超 24K，真实 argfile 启动未覆盖；Java 8 超长命令行未实现 wrapper（报 `E5003`） |

> 离线账号的 Realms 认证会报 `Failed to parse into SignedJWT`，属于预期（没有正版 token），不影响本地游戏。

## M2/M3 前端接线验收结果（B 部分，2026-10-24）

| 项 | 结果 |
|---|---|
| `loader-versions 1.21.1` | Fabric 255+ 个（首项 `0.20.1` 稳定）、Quilt 有列表、Forge **69 个**（如 `52.1.20`）、NeoForge **259 个**（如 `21.1.209`），版本号完整显示 |
| `install 1.21.1 --loader fabric --loader-version 0.16.10` | 成功；**78 个库**（原版 70 + Fabric profile 8：loader/intermediary/sponge-mixin/asm×5）；实例状态 `Ready` |
| `install 1.21.1`（原版，跳过资源） | 成功；秒装复用本地文件，最终 `Done` |
| 构建 | App / Core / CLI 全部 0 错误 |
| 测试 | Core 45 项、App 4 项通过 |
| 已知 | Forge / NeoForge 仅列出版本号，一键安装需官方 installer + Java 执行，尚未接入；NeoForge 官方 maven 在本机不可达，元数据改走 BMCLAPI 镜像 |

> 安装链路：`LoaderMetaClient` → `VersionResolver` 合并原版 + 加载器 profile → `VanillaInstaller` 下载库 → 实例写入 `Loader/LoaderVersion` → `LaunchService` 按加载器解析启动。

## M2 任务中心验收结果（B 部分，2026-10-24）

| 项 | 结果 |
|---|---|
| 任务入口 | 左侧底部「任务」按钮（PCL2 风格大按钮，与「更多」并列），进入即刷新并每秒轮询进行中任务 |
| 任务列表 | 类型 / 实例名 / 状态胶囊 / 进度条 / 百分比 / 尝试次数 / 最近消息；进行中排在最前 |
| `Percent = -1` | 进度条 `IsIndeterminate`，文案「准备中…」；下载页安装进度条同样支持不确定态 |
| 取消 / 重试 | 进行中可取消；失败 / 取消可重试（`Attempts + 1`）；中断恢复的任务不提供重试，只提供「修复」 |
| 启动恢复 | Core `TaskRegistry` 新增 SQLite `tasks` 表（schema v4）；重启后 `Running/Queued/Paused` 标记为失败（`E3004`），实例类任务可直接修复 |
| 测试 | Core **47 项**、App **9 项**通过（新增任务中心 5 项、TaskRegistry 落库/恢复 2 项） |
| 构建 | App 0 错误（3 条既有 `Watermark` 过时警告） |
| 实测 | 真实 Core 模式任务中心空态正常；Fake 模式取消后任务转「已取消」并可重试 |
| 顺带修复 | 下载页右侧分类栏只显示 `ModCategory { … }` 原始记录：`ListBoxItem` 模板的 `ContentPresenter` 未转发 `ContentTemplate`，补上后恢复中文分类名 |

> 任务中心的 VM 只调用 `ILauncherFacade`，不做文件 IO；恢复语义由 Core 负责。

## 目标样本

| 启动器 | 路径 | 大小 | 版本 |
|---|---|---|---|
| Plain Craft Launcher 2 | `C:\Users\lenovo\Desktop\Plain Craft Launcher 2.exe` | 5.75 MB | 2.13.1.1 |
| HMCL | `D:\HMCL-3.6.12\HMCL-3.16.3.exe` | 10.9 MB | 外壳 3.7.0.1 / 应用 3.16.4 |
| VersePC | `C:\Users\lenovo\AppData\Local\Programs\VersePC\VersePC.exe` | 223 MB | 1.1.17 |

## 核心结论（速查）

- PCL2：32 位 **VB.NET + WPF（.NET Framework 4.0 CLR）** 单文件 exe；依赖 DLL 全部以 .NET 资源内嵌（Newtonsoft.Json、NAudio、CacheCow、MeloongCore、libwebp64 等）；启动时落盘 `JavaWrapper.jar`(oolloo/jlw)、`libjlw-*.dll`、`Setup.ini`、`LatestLaunch.bat`、`Log1..5.txt`。用 `java -cp ... -jar JavaWrapper.jar <realMainClass>` 规避 Windows 命令行长度限制。
- HMCL：**Java + JavaFX** 应用；exe = `HMCLauncher.exe`(454KB 原生引导器) + 追加 ZIP(10.4MB, 4248 条目)，`Main-Class=org.jackhuang.hmcl.Main`。引导器负责找 Java（HMCL_JAVA_HOME/JAVA_HOME/注册表/bundled jre/下载 JDK）再拉起 JavaFX 主程序；内置 authlib-injector、lwjgl-unsafe-agent、MultiMC bootstrap、Terracotta。
- VersePC：**Electron + 原生 JS 前端 + Node 业务后端**；`resources/app.asar` 133MB(2931 条目)，`main.js` 通过自定义协议 `versepc://` 加载 UI，业务全在 `server.js`(1.1MB)，另有 `agent-engine.js` 多模型 AI Agent、插件系统（modrinth/mod-dev-tools）、monaco/xterm/three.js。Forge 安装复用 `forge-installer.jar`（com.bangbang93.ForgeInstaller，与 PCL2 内嵌的同一个类）。
- 签名现状（2026-10-24 用 `Get-AuthenticodeSignature` 复核）：三个样本 exe 全部 `NotSigned`；HMCL 另有**应用层**更新签名公钥 `assets/hmcl_signature_publickey.der`，PCL2 内置「Microsoft Defender 添加排除项」帮助文档——即行业普遍用"运行时引导 + 自身更新校验"替代 Authenticode。
- PCL2 许可证（2026-10-24 核实 `Meloong-Git/PCL`）：**不是 MIT/Apache**，而是《PCL 分发有限许可》+《存储库合理使用指南》。"重度使用"（基于仓库修改，或参考大量内容重制启动/下载/Mod 管理等实质功能）要求：名称以 `Plain Craft Launcher (PCL)` 开头、关于页首位署名并附赞助链接、公开源码并沿用其指南、实现正版/赞助劝导、Windows 主色相必须为蓝色。**与本项目 MIT + `Lumen` 不兼容，结论：不 fork**。

## 取证产物（仅本地，**不入库、不作为交付物**）

- `_probe/*.py`：PE / 字符串 / asar / zip 解析脚本
- `_probe/versepc/`：VersePC 主进程、后端、预加载、插件清单、AI 配置等抽取文件
- `_probe/hmcl/`：HMCLauncher.exe、hmcl.properties、boot.properties、openjfx 清单等
- 真实启动命令样本：`C:\Users\lenovo\Desktop\PCL\LatestLaunch.bat`

## 协作进度日志（唯一载体）

```
[2026-10-24] 调研 + 技术规划 + technoledge v0.3 完成。
             决策：代号 Lumen；A=核心 / B=UI 保持；GitHub 私有 + SSH 公钥；不签名仅 SHA-256；MIT 许可；
                   Forge ≤1.12.2 用 bangbang93（许可证为发布门禁）。
             下一步：M0 骨架（按用户要求暂不搭建）。

[2026-10-24] 已生成开发用 SSH 密钥（ed25519，无口令）：
             私钥 C:\Users\lenovo\.ssh\lumen_ed25519（未入库、未输出到对话）
             公钥指纹 SHA256:FxVW/gbMficPPOxpE+7o6QhqXhXBTY90st1ATUODmGM
             待朋友把公钥加到 GitHub（个人 SSH key 或仓库 Deploy key，二选一）。

[2026-10-24] SSH 已配置：`~/.ssh/config` 新增 `Host github.com`（IdentityFile=lumen_ed25519，
             IdentitiesOnly=yes），原配置备份为 `config.bak-20261024`。
             已验证：握手已提供该公钥；当前 GitHub 返回 `Permission denied (publickey)`，
             属预期——公钥尚未加入账号/仓库。

[2026-10-24] technoledge 升至 v0.4：
             数据目录拆 Roaming/Local；argfile 等首行输出后删 + 按 JVM 编码写；
             完整性改为"快校验 + mtime 缓存"；门面补 StopGame/GetTasks/GetInstance/SetActiveAccount 等；
             CF 决策写入 §6.3.1（默认 C，待拍板）。仍未开始搭建。

[2026-10-24] 仓库连接实测通过：
             SSH 认证返回 `Hi limpidus-xiaoche/Lumen!`（说明公钥以 Deploy key 绑定到该仓库）；
             `git ls-remote` 退出码 0 且无 ref → 仓库可读但为空；
             本地 `d:\启动器` 尚未 git init。未执行任何提交或推送。

[2026-10-24] 仓库已接通（写权限验证通过）：
             本地已 git init（main），首个提交 c2d2f65（README/technoledge/.gitignore/.gitattributes）；
             远程 origin = git@github.com:limpidus-xiaoche/Lumen.git，main 已推送并跟踪；
              `_probe/` 已加入 .gitignore，不提交；提交身份为仓库级 ttss4096 <ttss4096@outlook.com>（未改全局）。

[2026-10-24] 许可证核实（PCL2）：仓库采用《PCL 分发有限许可》+《合理使用指南》，非 MIT/Apache；
             重度使用条款（PCL 开头命名/蓝色主色/署名+赞助/沿用指南）与本项目 MIT+Lumen 冲突；
             结论：不 fork、不复制代码与资源；改为评估 CmlLib.Core（MIT）复用元数据/下载/启动原语。

[2026-10-24] 评估 XMCL（X Minecraft Launcher，voxelum，MIT，Electron + TypeScript）：
             许可证允许 fork/改名/闭源分发，只需保留版权与许可声明；
             其 @xmcl/core、installer、user、curseforge、modrinth、file-transfer 等包覆盖启动器核心；
             但与 Lumen 的 .NET/Avalonia 栈不兼容——直接复用需整体转 Node/Electron，或增加 Node sidecar。
             结论（已拍板）：**放弃 fork XMCL，维持路线 C**（.NET + CmlLib.Core）；XMCL 仅作 UX 参考。

[2026-10-24] technoledge v0.5 回写完成：
             CmlLib 能力边界表 + M0 门禁；协作身份记录为**临时 deploy key**（目标仍是 collaborators + 用户级 SSH）；
             PCL2 许可原文存档 docs/legal/；argfile 改 CodePages + 静态决定 + 缓存；
             移除 MCBBS、源表改 sources.json；补 Cancel/Retry/GetRunningSessions 与全部 DTO；
             下载 URL 白名单/私网拒绝；java-runtime 只取 raw；发布形态/R2R、integrity CI 生成、路径校验与 blobs GC。
             仓库协作收尾待 owner 操作（加 collaborators、降权 deploy key）。

[2026-10-24] M0 完成：
             solution + Launcher.Protocol / Core / App / Cli；SQLite v3 + 迁移入口；离线 UUID/伪 token；
             六页 Avalonia 壳 + FakeLauncherFacade；Serilog；单实例互斥；LICENSE / THIRD_PARTY_NOTICES / 许可存档；
             build 0 警告 0 错误；8 项测试通过；CLI 与 GUI 冒烟通过；pre-push hook 文件已提交。

[2026-10-24] 协作方式临时调整：`ttss4096` 账号暂不可用，继续使用仓库 deploy key 推送；
             工作流改为「推 feat/* 分支 → owner 在网页端合并」，不直推 main；
             账号恢复后再迁移到 collaborators + 用户级 SSH，deploy key 降为 CI 只读。

[2026-10-24] B（UI）M0 深化（feat/ui-m0）：
             深/浅双主题资源字典（App.axaml，全部颜色 DynamicResource，可一键换肤）；
             主窗口框架：左侧导航栏 + 内容区，选中高亮，底部低调水印「麦谷 & Limpidus」；
             设置页新增深色模式切换（M0 占位，直接生效）；FakeLauncherFacade 进度对齐契约
             （200ms/次，前两阶段 Percent=-1 不确定态）；build 0 警告 0 错误，App.Tests 2 项通过；
             hook 生效验证：直推 main 被本地拒绝；待 owner 网页端合并 feat/ui-m0。

[2026-10-24] B（UI）window-chrome 定稿：
             无边框窗口 920×560（自定义标题栏：麦谷Limpidus 亮青标识 + 最小化/关闭）；
             账号区改为右上角竖排卡片（模式 pill 放大 15px + 80px 头像 + 用户名）；底部版本号 Lumen 0.1 · M0。

[2026-10-24] M0 App 归属确认：
             A 代搭 `src/Launcher.App` 与 `tests/Launcher.App.Tests` 的最小脚手架（六页 + Fake + 深色主题）；
             B 接管，可整体重写；自 B 首次提交起，App 路径只由 B 修改，A 不再改动。

[2026-10-24] 前端策略更新：App 归属仍为 B；后续由 A 提供**最小基础模型**（页面骨架/绑定/数据接口），
             B 负责优化与设计定稿；A 只在 B 明确请求时修改 App。

[2026-10-24] M1（A）完成：
             ManifestClient（官方 + BMCLAPI fallback + 15 分钟缓存，正确解析 latest.release/snapshot）；
             VersionResolver（inheritsFrom 递归合并、库按 group:artifact 去重、参数拼接）；
             RuleEvaluator（os.name/arch/version/features）；JavaLocator（JAVA_HOME/注册表 32+64/常见目录/PATH/内置 runtime）；
             JavaVersionParser（release 优先、属性回退）；CLI 增加 versions / java / resolve；26 项测试通过。

[2026-10-24] M1 前端基础模型：
             App 改用真实 LauncherFacade（CoreServices 组装路径/设置/SQLite/manifest/Java）；
             下载页加载 manifest 并列出真实版本；设置页加载并显示本机 Java；
             新增 2 项 ViewModel 测试（总计 28 项）；视觉与交互待 B 优化。

[2026-10-24] M2（A）完成：
             DownloadEngine（16MB 阈值分片、8MB/片、Range 续传、顺序 SHA1、镜像回退、URL 安全校验、逐跳重定向）；
             VanillaInstaller（client jar/libraries/natives/assets、legacy virtual、磁盘预检、install journal、状态机）；
             TaskRegistry + ThrottledProgress（10Hz）；Facade install/repair/cancel/retry/getTasks；
             CLI install/repair；35 项测试通过；1.21.1（787.6 MB 资源）与 1.12.2 完整安装、秒装、修复均实测通过。

[2026-10-24] M3（A）完成：
             ArgumentBuilder（legacy/现代参数、rules、变量展开、未解析 E5002、classpath）；
             JavaVersionPolicy（版本约束 + 1.13+ 64 位过滤）；ArgFileWriter（ANSI/UTF-8、转义、首行后删除、清扫）；
             LaunchService（进程启动、stdout/stderr、退出归因、RunningSessionStore 持久化）；CLI launch/stop/sessions；
             48 项测试通过；1.21.1 真实启动、会话可见、Graceful 停止 exit 0 已实测。

[2026-10-24] 默认游戏语言：
             `GameOptions.EnsureDefaultLanguage` 在安装与启动前检查 `options.txt`，缺失时写入 `lang:zh_cn`；
             已有文件不覆盖（尊重用户改动）；当前实例已修正并在实机启动/退出后验证仍为 zh_cn；测试增至 50 项。

[2026-10-24] M1 合并（feat/m1-merge）：
             合入 window-chrome（B 布局定稿）+ m3-core（A 全部核心 M1-M3）；
             设计以 B 为准，数据接 A 真实门面：下载页真实版本清单、设置页真实 Java 检测；
             Home 启动按钮 M1 暂不接真实启动（M2 安装 UI 未接），待 M3 阶段接线。

[2026-10-24] B（UI）M1 页面落地：
             版本设置页改为横向分类（概览 / 启动 / Mod 管理），「启动」栏新增 Java 环境选择，
             下拉项显示版本（Java 17 / 21…）与完整文件路径，保存时写入 settings.JavaPath；
             Mod 管理支持扫描 / 启用 / 禁用 / 删除 / 添加 jar：从 fabric.mod.json 提取 id、名称、版本，
             并提取 jar 内 icon.png（或 logo.png / pack.png）作为图标，取不到时回退首字母方块；
             左侧底部新增「更多」：关于、免责声明（Minecraft/加载器/模组版权归属）、功能对接状态、项目主页；
             窗口保持无边框 820×560 紧凑布局，右上角竖排账号卡片（离线 / 正版 pill + 80px 头像）。

[2026-10-24] 模组下载体验迭代：
             模组名中英并显（内置 40+ 常用模组译名表，未收录则显示英文原名）；
             右侧「分类」栏可收纳（性能优化 / 冒险 / 科技 / 魔法 / 装饰 / 工具 / 世界生成 / 存储 / 生物 / 装备 / 前置库），
             对应 Modrinth facets 的 categories 过滤；结果改为固定高度独立滚动 + 上一页 / 下一页分页（每页 20 条），
             避免长列表把分类栏顶出视野；账号卡片后续已迁移到启动页，不再占用下载页。
             下载页分「原版游戏 / 模组」两栏；模组栏进入即按下载量加载热门列表，无需先搜索；
             已接入 **Modrinth 开放 API**：搜索、按当前实例加载器与游戏版本挑选文件、图标缓存、安装到 mods；
             已接入 **CurseForge 官方 API**：同一套 UI 可切换渠道，需设置环境变量 `LUMEN_CURSEFORGE_KEY` 后重启；
             作者关闭第三方下载的 CF 文件会提示「请前往 CurseForge 页面手动下载」，不会静默失败。

[2026-10-24] M2/M3 前端接线（B）：
             下载页原版游戏：正式版 / 快照 / 全部筛选 + 版本号搜索 + 中文类型标识 + 安装进度条（阶段中文映射）；
             新增 `LoaderMetaClient`：Fabric / Quilt 官方 meta、Forge maven 元数据 + promotions 推荐标记、
             NeoForge（BMCLAPI 镜像；1.20.1 走 legacy net/neoforged/forge 路径）；
             `VersionResolver` 支持「原版 + 加载器 profile」合并，`VanillaInstaller` / `LaunchService` 按实例 Loader/LoaderVersion 解析；
             库下载支持 profile 的仓库根地址（`maven.fabricmc.net` / `maven.quiltmc.org` 已加入白名单）；
             加载器区块：选加载器与游戏版本即列出完整版本号（稳定 / 历史标识），Fabric / Quilt 可一键安装；
             Home 账号区（离线 / 正版 pill + 64px 头像 + 用户名）移至启动大按钮上方，移除右上角账号卡片；
             CLI 新增 `loader-versions`、`install --loader/--loader-version`；实测 Fabric 0.16.10 + 1.21.1 安装成功（78 库）。

[2026-10-24] M2 任务中心（B）：
             左侧底部新增「任务」入口；任务中心页展示类型 / 实例 / 状态 / 进度 / 尝试次数与消息，
             进行中任务每秒轮询，支持取消、重试与实例类任务修复；`Percent=-1` 统一走不确定进度条（任务中心 + 下载页）；
             Core `TaskRegistry` 接入 SQLite `tasks` 表（schema v4），重启后未完成任务标记为 `E3004`（可修复），
             历史任务保留最近 50 条；`TaskInfo` 契约新增 `TaskErrorCodes.Interrupted`；
             顺带修复下载页分类栏 `ContentPresenter` 未转发 `ContentTemplate` 导致显示原始记录的问题。
```
