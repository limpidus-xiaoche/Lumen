using System.IO.Compression;
using System.Text.Json;
using Launcher.Core.Download;
using Launcher.Core.Game;
using Launcher.Core.Storage;
using Launcher.Protocol;
using Microsoft.Extensions.Logging;

namespace Launcher.Core.Install;

/// <summary>
/// 整合包导入器。支持 Modrinth / HMCL / MultiMC，CurseForge 仅直链包（不调 CF API）。
/// 流程：识别 → 解析清单 → 安装原版+加载器 → 下载清单声明的依赖 → 复制 overrides。
/// </summary>
public sealed class ModpackInstaller
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
    };

    private readonly LauncherPaths _paths;
    private readonly DownloadEngine _downloads;
    private readonly VanillaInstaller _installer;
    private readonly SqliteStore _sqlite;
    private readonly ILogger<ModpackInstaller> _logger;

    public ModpackInstaller(
        LauncherPaths paths,
        DownloadEngine downloads,
        VanillaInstaller installer,
        SqliteStore sqlite,
        ILogger<ModpackInstaller> logger)
    {
        _paths = paths;
        _downloads = downloads;
        _installer = installer;
        _sqlite = sqlite;
        _logger = logger;
    }

    public async Task<InstanceSummary> InstallAsync(
        ModpackInstallRequest request,
        IProgress<InstallProgress> progress,
        CancellationToken ct)
    {
        var (packDir, ownsTemp) = EnsureExtracted(request.FilePath);
        try
        {
            var kind = request.Kind == ModpackKind.CurseForgeDirect
                ? ModpackKind.CurseForgeDirect
                : DetectKind(packDir);
            var parsed = Parse(packDir, kind);

            var instanceId = BuildInstanceId(request.InstanceName ?? parsed.Name);
            var isolation = request.Isolation;
            var gameDir = isolation == IsolationLevel.None
                ? Path.Combine(_paths.SharedDir, "game")
                : Path.Combine(_paths.InstancesDir, instanceId);

            Report(progress, InstallStage.VersionJson, 5, $"识别整合包：{parsed.Name} ({kind})");

            // 1. 安装原版 + 加载器
            var installRequest = new InstallRequest(
                InstanceId: instanceId,
                DisplayName: request.InstanceName ?? parsed.Name,
                VersionId: parsed.GameVersion,
                Loader: parsed.Loader,
                LoaderVersion: parsed.LoaderVersion,
                Isolation: isolation,
                Mode: InstallMode.New);

            var summary = await _installer.InstallAsync(installRequest, progress, ct);

            // 2. 下载清单声明的依赖
            if (parsed.Files.Count > 0)
            {
                Report(progress, InstallStage.Libraries, 0, $"下载 {parsed.Files.Count} 个整合包文件");
                await DownloadFilesAsync(parsed.Files, gameDir, progress, ct);
                Report(progress, InstallStage.Libraries, 100, "整合包文件下载完成");
            }

            // 3. 复制 overrides
            if (!string.IsNullOrEmpty(parsed.OverridesDir) && Directory.Exists(parsed.OverridesDir))
            {
                Report(progress, InstallStage.Finalizing, 50, "复制整合包配置");
                CopyOverrides(parsed.OverridesDir, gameDir);
            }

            // 4. 默认中文
            GameOptions.EnsureDefaultLanguage(gameDir);

            await _sqlite.UpdateInstanceStateAsync(instanceId, InstanceState.Ready, ct);
            Report(progress, InstallStage.Done, 100, "整合包安装完成");

            _logger.LogInformation("整合包安装完成：{Name} -> {Instance}", parsed.Name, instanceId);
            return summary with { Id = instanceId, Name = request.InstanceName ?? parsed.Name };
        }
        finally
        {
            if (ownsTemp && Directory.Exists(packDir))
            {
                try { Directory.Delete(packDir, recursive: true); } catch { /* 忽略清扫失败 */ }
            }
        }
    }

    private (string Dir, bool OwnsTemp) EnsureExtracted(string path)
    {
        var full = Path.GetFullPath(path);
        if (Directory.Exists(full))
        {
            return (full, false);
        }

        if (!File.Exists(full))
        {
            throw new InstallException("E3004", $"整合包路径不存在：{full}");
        }

        var ext = Path.GetExtension(full).ToLowerInvariant();
        if (ext is not ".zip" and not ".mrpack" and not ".mmc" and not ".hmcl")
        {
            throw new InstallException("E3004", $"不支持的整合包格式：{ext}（仅支持 .zip/.mrpack/.mmc/.hmcl）");
        }

        var temp = Path.Combine(_paths.CacheDir, $"modpack-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        try
        {
            ZipFile.ExtractToDirectory(full, temp);
        }
        catch (Exception ex)
        {
            Directory.Delete(temp, recursive: true);
            throw new InstallException("E3004", $"解压整合包失败：{ex.Message}", ex);
        }

        return (temp, true);
    }

    public static ModpackKind DetectKind(string dir)
    {
        if (File.Exists(Path.Combine(dir, "modrinth.index.json"))) return ModpackKind.Modrinth;
        if (File.Exists(Path.Combine(dir, "mmc-pack.json"))) return ModpackKind.MultiMc;
        if (File.Exists(Path.Combine(dir, "modpack.json"))) return ModpackKind.Hmcl;
        if (File.Exists(Path.Combine(dir, "manifest.json"))) return ModpackKind.CurseForgeDirect;
        throw new InstallException("E3004", "无法识别整合包：缺少 modrinth.index.json / mmc-pack.json / modpack.json / manifest.json");
    }

    public static ParsedModpack Parse(string dir, ModpackKind kind) => kind switch
    {
        ModpackKind.Modrinth => ParseModrinth(dir),
        ModpackKind.Hmcl => ParseHmcl(dir),
        ModpackKind.MultiMc => ParseMultiMc(dir),
        ModpackKind.CurseForgeDirect => ParseCurseForge(dir),
        _ => throw new InstallException("E3004", $"未知的整合包类型：{kind}"),
    };

    private static ParsedModpack ParseModrinth(string dir)
    {
        var indexPath = Path.Combine(dir, "modrinth.index.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(indexPath));
        var root = doc.RootElement;

        var name = GetString(root, "name") ?? Path.GetFileName(dir);
        var gameVersion = string.Empty;
        LoaderKind loader = LoaderKind.Vanilla;
        string? loaderVersion = null;

        if (root.TryGetProperty("dependencies", out var deps))
        {
            gameVersion = GetString(deps, "minecraft") ?? string.Empty;
            if (deps.TryGetProperty("fabric-loader", out var fabric))
            {
                loader = LoaderKind.Fabric;
                loaderVersion = fabric.GetString();
            }
            else if (deps.TryGetProperty("forge", out var forge))
            {
                loader = LoaderKind.Forge;
                loaderVersion = forge.GetString();
            }
            else if (deps.TryGetProperty("neoforge", out var neo))
            {
                loader = LoaderKind.NeoForge;
                loaderVersion = neo.GetString();
            }
        }

        if (string.IsNullOrEmpty(gameVersion))
        {
            throw new InstallException("E3004", "modrinth.index.json 缺少 dependencies.minecraft");
        }

        var files = new List<ModpackFile>();
        if (root.TryGetProperty("files", out var filesEl) && filesEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var file in filesEl.EnumerateArray())
            {
                var relPath = GetString(file, "path");
                var downloads = file.TryGetProperty("downloads", out var dl) && dl.ValueKind == JsonValueKind.Array
                    ? dl.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrEmpty(x)).ToList()!
                    : new List<string>();
                if (relPath is null || downloads.Count == 0)
                {
                    continue;
                }

                // 只安装 client 端需要的文件
                var envClient = "required";
                if (file.TryGetProperty("env", out var env) &&
                    env.TryGetProperty("client", out var client))
                {
                    envClient = client.GetString() ?? "required";
                }

                if (envClient == "unsupported")
                {
                    continue;
                }

                var sha1 = file.TryGetProperty("hashes", out var hashes)
                    ? GetString(hashes, "sha1")
                    : null;
                var size = file.TryGetProperty("fileSize", out var sizeEl) && sizeEl.TryGetInt64(out var s) ? s : 0;

                files.Add(new ModpackFile(relPath, downloads[0]!, downloads.Skip(1).ToList(), sha1, size));
            }
        }

        var overrides = FirstExisting(dir, "overrides");
        return new ParsedModpack(ModpackKind.Modrinth, name, gameVersion, loader, loaderVersion, files, overrides);
    }

    private static ParsedModpack ParseHmcl(string dir)
    {
        var jsonPath = Path.Combine(dir, "modpack.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var root = doc.RootElement;

        var name = GetString(root, "name") ?? Path.GetFileName(dir);
        var gameVersion = GetString(root, "gameVersion")
            ?? GetString(root, "minecraftVersion")
            ?? throw new InstallException("E3004", "modpack.json 缺少 gameVersion");

        LoaderKind loader = LoaderKind.Vanilla;
        string? loaderVersion = null;
        if (root.TryGetProperty("modLoader", out var ml))
        {
            var mlStr = ml.GetString() ?? string.Empty;
            if (mlStr.StartsWith("fabric", StringComparison.OrdinalIgnoreCase))
            {
                loader = LoaderKind.Fabric;
                loaderVersion = GetString(root, "modLoaderVersion");
            }
            else if (mlStr.StartsWith("forge", StringComparison.OrdinalIgnoreCase))
            {
                loader = LoaderKind.Forge;
                loaderVersion = GetString(root, "modLoaderVersion");
            }
        }

        var files = new List<ModpackFile>();
        if (root.TryGetProperty("files", out var filesEl) && filesEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var file in filesEl.EnumerateArray())
            {
                var url = GetString(file, "url");
                var path = GetString(file, "path");
                if (url is null || path is null)
                {
                    continue;
                }

                var sha1 = GetString(file, "sha1");
                var size = file.TryGetProperty("size", out var sizeEl) && sizeEl.TryGetInt64(out var s) ? s : 0;
                files.Add(new ModpackFile(path, url, null, sha1, size));
            }
        }

        var overrides = FirstExisting(dir, "overrides");
        return new ParsedModpack(ModpackKind.Hmcl, name, gameVersion, loader, loaderVersion, files, overrides);
    }

    private static ParsedModpack ParseMultiMc(string dir)
    {
        var jsonPath = Path.Combine(dir, "mmc-pack.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var root = doc.RootElement;

        var gameVersion = string.Empty;
        LoaderKind loader = LoaderKind.Vanilla;
        string? loaderVersion = null;

        if (root.TryGetProperty("components", out var components) && components.ValueKind == JsonValueKind.Array)
        {
            foreach (var component in components.EnumerateArray())
            {
                var uid = GetString(component, "uid");
                var version = GetString(component, "version");
                if (uid is null || version is null) continue;

                switch (uid)
                {
                    case "net.minecraft":
                        gameVersion = version;
                        break;
                    case "net.fabricmc.fabric-loader":
                        loader = LoaderKind.Fabric;
                        loaderVersion = version;
                        break;
                    case "net.minecraftforge":
                        loader = LoaderKind.Forge;
                        loaderVersion = version;
                        break;
                    case "net.neoforged":
                        loader = LoaderKind.NeoForge;
                        loaderVersion = version;
                        break;
                }
            }
        }

        if (string.IsNullOrEmpty(gameVersion))
        {
            throw new InstallException("E3004", "mmc-pack.json 缺少 net.minecraft 组件");
        }

        var name = ReadMultiMcName(dir) ?? Path.GetFileName(dir);

        // MultiMC 的 minecraft/ 目录即为 overrides
        var overrides = FirstExisting(dir, "minecraft", ".minecraft");
        return new ParsedModpack(ModpackKind.MultiMc, name, gameVersion, loader, loaderVersion, [], overrides);
    }

    private static ParsedModpack ParseCurseForge(string dir)
    {
        var jsonPath = Path.Combine(dir, "manifest.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var root = doc.RootElement;

        var name = GetString(root, "name") ?? Path.GetFileName(dir);
        string gameVersion;
        LoaderKind loader = LoaderKind.Vanilla;
        string? loaderVersion = null;

        if (root.TryGetProperty("minecraft", out var mc))
        {
            gameVersion = GetString(mc, "version") ?? string.Empty;
            if (mc.TryGetProperty("modLoaders", out var modLoaders) && modLoaders.ValueKind == JsonValueKind.Array)
            {
                foreach (var ml in modLoaders.EnumerateArray())
                {
                    var id = GetString(ml, "id");
                    if (id is null) continue;
                    if (id.StartsWith("forge-", StringComparison.OrdinalIgnoreCase))
                    {
                        loader = LoaderKind.Forge;
                        loaderVersion = id["forge-".Length..];
                    }
                    else if (id.StartsWith("neoforge-", StringComparison.OrdinalIgnoreCase))
                    {
                        loader = LoaderKind.NeoForge;
                        loaderVersion = id["neoforge-".Length..];
                    }
                    else if (id.StartsWith("fabric-", StringComparison.OrdinalIgnoreCase))
                    {
                        loader = LoaderKind.Fabric;
                        loaderVersion = id["fabric-".Length..];
                    }
                }
            }
        }
        else
        {
            throw new InstallException("E3004", "manifest.json 缺少 minecraft 字段");
        }

        if (string.IsNullOrEmpty(gameVersion))
        {
            throw new InstallException("E3004", "manifest.json 缺少 minecraft.version");
        }

        // CF 仅直链包：files[] 只含 projectID/fileID，无直链 URL，跳过下载
        // 若将来扩展，需调用 CF API 解析；当前 M7 不验收
        var files = new List<ModpackFile>();

        var overridesName = GetString(root, "overrides") ?? "overrides";
        var overrides = FirstExisting(dir, overridesName);
        return new ParsedModpack(ModpackKind.CurseForgeDirect, name, gameVersion, loader, loaderVersion, files, overrides);
    }

    private async Task DownloadFilesAsync(
        IReadOnlyList<ModpackFile> files,
        string gameDir,
        IProgress<InstallProgress> progress,
        CancellationToken ct)
    {
        var total = files.Count;
        var done = 0;

        await Parallel.ForEachAsync(
            files,
            new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = ct },
            async (file, token) =>
            {
                var dest = PathGuard.EnsureWithin(gameDir, Path.Combine(gameDir, file.Path.Replace('/', Path.DirectorySeparatorChar)));
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

                try
                {
                    UrlGuard.Validate(file.Url);
                    await _downloads.DownloadAsync(
                        new DownloadItem(file.Url, dest, file.Sha1, file.Size, DownloadKind.Other, file.Mirrors),
                        null, token);
                }
                catch (DownloadException ex) when (ex.ErrorCode == "E2001" && file.Sha1 is null)
                {
                    // 无 hash 且非白名单 host 的直链拒绝，跳过并记录
                    _logger.LogWarning("跳过不安全的整合包文件（无 hash 且非白名单）：{Path} {Url}", file.Path, file.Url);
                }

                var completed = Interlocked.Increment(ref done);
                Report(progress, InstallStage.Libraries, completed * 100d / total,
                    $"{completed}/{total}", file.Path);
            });
    }

    private static void CopyOverrides(string source, string gameDir)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = PathGuard.EnsureWithin(gameDir, Path.Combine(gameDir, relative));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static string BuildInstanceId(string name)
    {
        var slug = new string(name.Select(c =>
            char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '-').ToArray()).Trim('-');
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = "modpack";
        }

        if (slug.Length > 24)
        {
            slug = slug[..24];
        }

        var suffix = Guid.NewGuid().ToString("N")[..6];
        return PathGuard.ValidateId($"{slug}-{suffix}", nameof(name));
    }

    private static string? ReadMultiMcName(string root)
    {
        var cfg = Path.Combine(root, "instance.cfg");
        if (!File.Exists(cfg)) return null;
        foreach (var line in File.ReadLines(cfg))
        {
            if (line.StartsWith("name=", StringComparison.OrdinalIgnoreCase))
            {
                return line["name=".Length..].Trim();
            }
        }

        return null;
    }

    private static string? FirstExisting(string root, params string[] names) =>
        names.Select(n => Path.Combine(root, n)).FirstOrDefault(Directory.Exists);

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static void Report(
        IProgress<InstallProgress> progress,
        InstallStage stage,
        double percent,
        string message,
        string? currentFile = null)
    {
        progress.Report(new InstallProgress(stage, percent, 0, 0, 0, currentFile, message));
    }

    public sealed record ParsedModpack(
        ModpackKind Kind,
        string Name,
        string GameVersion,
        LoaderKind Loader,
        string? LoaderVersion,
        IReadOnlyList<ModpackFile> Files,
        string? OverridesDir);

    public sealed record ModpackFile(
        string Path,
        string Url,
        IReadOnlyList<string>? Mirrors,
        string? Sha1,
        long Size);
}
