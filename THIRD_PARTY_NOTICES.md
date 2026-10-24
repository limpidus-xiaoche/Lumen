# 第三方组件声明

本项目采用 MIT 许可证。以下组件随包分发或作为构建/测试依赖使用；引入前需复核许可证，
不兼容 MIT 的组件不得随包分发，只能运行期下载或调用。

## 随包分发

| 组件 | 许可证 | 用途 |
|---|---|---|
| Avalonia 12 | MIT | UI 框架 |
| CommunityToolkit.Mvvm | MIT | MVVM |
| Serilog + Serilog.Sinks.File | Apache-2.0 | 日志 |
| Microsoft.Data.Sqlite / SQLitePCLRaw | MIT / Apache-2.0 | 本地索引 |
| Polly.Core | BSD-3-Clause | 下载/网络韧性 |
| System.Security.Cryptography.ProtectedData | MIT | DPAPI 凭据保护 |

## 运行期下载，不随包分发

| 组件 | 许可证（待复核） | 用途 |
|---|---|---|
| authlib-injector | AGPL-3.0 | 外置登录；仅运行期下载并校验 SHA1，附源码链接 |
| Forge / NeoForge installer | LGPL-2.1（待复核） | 加载器安装；**运行期**从官方 maven 下载，不随包分发 |

## 仅开发/测试

| 组件 | 许可证 | 用途 |
|---|---|---|
| xunit / xunit.runner.visualstudio | Apache-2.0 | 单元测试 |
| Microsoft.NET.Test.Sdk | MIT | 测试宿主 |
| NetArchTest.Rules | MIT | 架构测试 |

## 明确不包含

- 任何 PCL2（Plain Craft Launcher）的代码、资源或帮助文本。
- Minecraft 客户端 jar、assets 或原版文件（全部运行期从官方/镜像下载）。
