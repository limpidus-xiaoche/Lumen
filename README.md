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
```

## 待办（下一步）

- 发布流程：先打一个旧版本 tag 走 `release.yml`，再做「旧版 → 新版」端到端升级实测（当前只验证了打包与校验链路）。
- B 接管 `src/Launcher.App`：M2–M8 的 UI 部分（任务中心、实例卡片、账户页、安装向导、导入向导、更新入口、首次启动引导、日志轮转设置）。
- 协作收尾：`ttss4096` 账号恢复后，把 deploy key 降为 CI 只读，开发者改用用户级 SSH key。
