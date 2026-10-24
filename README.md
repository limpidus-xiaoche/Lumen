# MC 启动器原理调研（本地取证）

> 本文件按用户规则实时记录进度。所有结论均来自本机文件静态分析，未使用网络资料。

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
- [x] `technoledge.md` **v0.3 复审**（2026-10-24）：新增 P0 5 项 / P1 16 项 / P2 11 项，见下「v0.3 复审记录」
- [x] `technoledge.md` 升版 **v0.4** 完成：P0-1..P0-5 全部落位（CF=C、GitHub=Free）
- [ ] M0：解决方案骨架（**用户要求暂不搭建**）

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
- 技术栈：`.NET 10 LTS`（环境不一致则统一 `.NET 8 LTS`，单一 TFM 由 `global.json` 钉死）+ `Avalonia 11` + `CommunityToolkit.Mvvm` + `Polly.Core`；`System.Text.Json` 源生成；SQLite 存索引，JSON 存配置。
- 形态：单进程 GUI + 可复用 `Launcher.Core` 类库 + `Launcher.Cli`（内核可被 CLI/测试直接驱动）；`Core` 不引用任何 UI 类型。
- 规避 32K 命令行：Java 9+ 用 `@argfile`（启动后立即删除 + ACL 收紧）；Java 8 回退 wrapper jar（对标 PCL2 JavaWrapper）。
- Forge/NeoForge：≤1.12.2 使用 `com.bangbang93.ForgeInstaller`（**许可证核验为发布门禁**），1.13+ 走官方 installer；`authlib-injector` 不随包分发。分发只用 Velopack（`Setup.exe` + delta + Portable zip），不产 MSI。
- 完整性：**不签名**（无 Authenticode、无应用层公钥签名），只用 **SHA-256 清单**（自身 `integrity.json` + 更新清单）并 fail-closed；安全边界与残余风险见 technoledge §12.1。
- 仓库：**GitHub 私有**，两位开发者各自 SSH 公钥（ed25519），`main` 保护 + 对方 1 approve，CODEOWNERS 按 A/B 目录划分。
- 许可证：本项目 **MIT**；随包第三方必须与 MIT 兼容，不兼容者仅运行期下载/调用，并登记 `THIRD_PARTY_NOTICES.md`。
- v1 范围：元数据 → 安装 → 启动 → Java 管理 → 离线/微软/外置登录 → 实例隔离 → 模组/整合包 → 自更新。AI/联机/皮肤站作为 v2。

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

## 取证产物

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
```
