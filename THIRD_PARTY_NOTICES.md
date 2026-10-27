# 第三方组件声明

本项目采用 MIT 许可证。以下组件随包分发或作为构建/测试依赖使用；引入前需复核许可证，
不兼容 MIT 的组件不得随包分发，只能运行期下载或调用。

包 ID 与版本以 [`Directory.Packages.props`](Directory.Packages.props) 及 `src/**/*.csproj`、`tests/**/*.csproj` 的
`PackageReference` 为准（中心包管理，csproj 内不写版本）。

## 随包分发

| 组件（包 ID） | 版本 | 许可证 | 用途 |
|---|---|---|---|
| `Avalonia`、`Avalonia.Desktop`、`Avalonia.Themes.Fluent` | 12.1.4 | MIT | UI 框架、桌面后端、Fluent 主题 |
| `Avalonia.Fonts.Inter` | 12.1.4 | MIT（包）；内置 Inter 字体为 OFL-1.1 | 默认字体 |
| `CommunityToolkit.Mvvm` | 8.4.2 | MIT | MVVM |
| `Serilog.Extensions.Logging`、`Serilog.Sinks.File` | 10.0.0 / 7.0.0 | Apache-2.0 | 日志接入与文件落盘 |
| `Microsoft.Data.Sqlite` | 10.0.13 | MIT（传递依赖 `SQLitePCLRaw` 为 Apache-2.0） | 本地索引 |
| `Polly.Core` | 8.8.0 | BSD-3-Clause | 下载/网络韧性 |
| `Microsoft.Extensions.Logging.Abstractions` | 10.0.13 | MIT | 日志抽象（Core 层） |
| `System.Security.Cryptography.ProtectedData` | 10.0.0 | MIT | DPAPI 凭据保护 |
| `System.CommandLine` | 2.0.13 | MIT | CLI 解析 |
| `Velopack` | 1.2.161 | MIT | 安装/增量更新/便携版打包 |
| `StbImageSharp` | 2.27.11 | Public Domain（stb_image） | 贴图解码 |

> `SharpGLTF.Core`、`NAudio` / `NAudio.Wasapi`、`NAudio.Vorbis` 尚未引入（S4/S5 计划），
> 落地时按同一格式补登许可证后再入包。

## 构建与测试

| 组件（包 ID） | 版本 | 许可证 | 用途 |
|---|---|---|---|
| `Microsoft.NET.Test.Sdk` | 18.11.0 | MIT | 测试宿主 |
| `xunit`、`xunit.runner.visualstudio` | 2.9.3 / 4.0.1 | Apache-2.0 | 单元测试与运行器 |
| `NetArchTest.Rules` | 1.3.2 | MIT | 架构测试 |

以上仅参与构建/测试，不随最终包分发。

## 运行期下载，不随包分发

| 组件 | 许可证（待复核） | 用途 |
|---|---|---|
| authlib-injector | AGPL-3.0 | 外置登录；仅运行期下载并校验 SHA1，附源码链接 |
| Forge / NeoForge installer | LGPL-2.1（待复核） | 加载器安装；**运行期**从官方 maven 下载，不随包分发 |

## 明确不包含

- 任何 PCL2（Plain Craft Launcher）的代码、资源或帮助文本。
- Minecraft 客户端 jar、assets、模型/贴图等原版文件（全部运行期从官方/镜像下载）。
- 任何 MSI 安装器（分发只用 Velopack：`Setup.exe` + delta + `Portable.zip`）。
