using Launcher.Protocol;

namespace Launcher.Core.Install;

/// <summary>
/// 加载器子版本的目录/JSON ID 规则。安装与启动必须使用同一套规则。
/// </summary>
public static class LoaderIds
{
    public static string EffectiveVersionId(string gameVersion, LoaderKind loader, string? loaderVersion) =>
        loader switch
        {
            LoaderKind.Vanilla => gameVersion,
            LoaderKind.Fabric => $"fabric-loader-{Require(loaderVersion, loader)}-{gameVersion}",
            LoaderKind.Forge => $"{gameVersion}-forge-{Require(loaderVersion, loader)}",
            LoaderKind.NeoForge => $"{gameVersion}-neoforge-{Require(loaderVersion, loader)}",
            _ => throw new InstallException("E3001", $"{loader} 尚未支持"),
        };

    public static bool IsLoader(LoaderKind loader) => loader != LoaderKind.Vanilla;

    private static string Require(string? loaderVersion, LoaderKind loader) =>
        string.IsNullOrWhiteSpace(loaderVersion)
            ? throw new InstallException("E3001", $"{loader} 需要指定加载器版本")
            : loaderVersion.Trim();
}
