using System.Text.Json;
using Launcher.Core.Install;
using Launcher.Core.Meta;
using Launcher.Core.Storage;
using Launcher.Protocol;

namespace Launcher.Core.Instances;

public sealed record ImportCandidate(
    string SourceKind,
    string RootPath,
    string GameDir,
    string GameVersion,
    LoaderKind Loader,
    string? LoaderVersion,
    string? Name);

/// <summary>
/// 外部实例只读识别 + 复制导入。识别 PCL/原生 `.minecraft`、MultiMC、HMCL modpack.json。
/// 只复制游戏内容（mods/config/saves 等），不复制 libraries/assets/versions，避免磁盘爆炸。
/// </summary>
public static class ExternalImporter
{
    private static readonly string[] CopyExclusions =
    [
        "libraries", "assets", "versions", "runtime", "logs", "crash-reports", "exports",
    ];

    public static ImportCandidate Detect(string path)
    {
        var root = Path.GetFullPath(path);
        if (!Directory.Exists(root))
        {
            throw new InstallException("E3004", $"目录不存在：{root}");
        }

        var multimc = Path.Combine(root, "mmc-pack.json");
        if (File.Exists(multimc))
        {
            return DetectMultiMc(root, multimc);
        }

        var hmcl = Path.Combine(root, "modpack.json");
        if (File.Exists(hmcl))
        {
            return DetectHmcl(root, hmcl);
        }

        var versionJson = FindVersionJson(root);
        if (versionJson is null)
        {
            throw new InstallException(
                "E3004", "无法识别实例：缺少 versions/<id>/<id>.json、mmc-pack.json 或 modpack.json");
        }

        var detail = ManifestClient.DeserializeDetail(File.ReadAllText(versionJson.Value.Json));
        var gameDir = versionJson.Value.IsVersionDir
            ? Path.GetDirectoryName(Path.GetDirectoryName(versionJson.Value.Json))!
            : root;
        var (loader, loaderVersion) = DetectLoader(detail.Id, detail.InheritsFrom);
        var gameVersion = detail.InheritsFrom ?? detail.Id;

        return new ImportCandidate(
            "vanilla", root, gameDir, gameVersion, loader, loaderVersion,
            Path.GetFileName(gameDir.TrimEnd(Path.DirectorySeparatorChar)));
    }

    public static async Task<string> CopyAsync(
        ImportCandidate candidate,
        LauncherPaths paths,
        string? displayName,
        IProgress<ImportProgress> progress,
        CancellationToken ct)
    {
        var instanceId = BuildInstanceId(displayName ?? candidate.Name ?? candidate.GameVersion);
        var destination = PathGuard.EnsureWithin(
            paths.InstancesDir, Path.Combine(paths.InstancesDir, instanceId));

        var files = EnumerateGameFiles(candidate.GameDir).ToArray();
        long totalBytes = files.Sum(f => SafeLength(f));

        Directory.CreateDirectory(destination);
        long copied = 0;
        var done = 0;
        progress.Report(new ImportProgress("copy", 0, 0, totalBytes, candidate.GameDir));

        foreach (var source in files)
        {
            ct.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(candidate.GameDir, source);
            var target = PathGuard.EnsureWithin(destination, Path.Combine(destination, relative));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using (var input = File.OpenRead(source))
            await using (var output = File.Create(target))
            {
                await input.CopyToAsync(output, ct);
            }

            copied += SafeLength(source);
            done++;
            progress.Report(new ImportProgress(
                "copy",
                totalBytes > 0 ? copied * 100d / totalBytes : done * 100d / Math.Max(1, files.Length),
                copied,
                totalBytes,
                relative));
        }

        progress.Report(new ImportProgress("done", 100, copied, totalBytes, destination));
        return instanceId;
    }

    private static ImportCandidate DetectMultiMc(string root, string packPath)
    {
        var document = JsonDocument.Parse(File.ReadAllText(packPath));
        string? game = null;
        LoaderKind loader = LoaderKind.Vanilla;
        string? loaderVersion = null;

        if (document.RootElement.TryGetProperty("components", out var components) &&
            components.ValueKind == JsonValueKind.Array)
        {
            foreach (var component in components.EnumerateArray())
            {
                var uid = component.TryGetProperty("uid", out var uidElement)
                    ? uidElement.GetString()
                    : null;
                var version = component.TryGetProperty("version", out var versionElement)
                    ? versionElement.GetString()
                    : null;
                if (uid is null || version is null)
                {
                    continue;
                }

                switch (uid)
                {
                    case "net.minecraft":
                        game = version;
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

        if (game is null)
        {
            throw new InstallException("E3004", "mmc-pack.json 缺少 net.minecraft 组件");
        }

        var gameDir = FirstExisting(root, "minecraft", ".minecraft") ?? root;
        var name = ReadMultiMcName(root) ?? Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar));
        return new ImportCandidate("multimc", root, gameDir, game, loader, loaderVersion, name);
    }

    private static ImportCandidate DetectHmcl(string root, string modpackPath)
    {
        var document = JsonDocument.Parse(File.ReadAllText(modpackPath));
        var game = GetString(document.RootElement, "gameVersion")
            ?? GetString(document.RootElement, "minecraftVersion")
            ?? throw new InstallException("E3004", "modpack.json 缺少 gameVersion，无法确定游戏版本");
        var name = GetString(document.RootElement, "name")
            ?? Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar));

        var gameDir = FirstExisting(root, "minecraft", ".minecraft") ?? root;
        return new ImportCandidate("hmcl", root, gameDir, game, LoaderKind.Vanilla, null, name);
    }

    private static (string Json, bool IsVersionDir)? FindVersionJson(string root)
    {
        var self = Path.Combine(root, $"{Path.GetFileName(root)}.json");
        if (File.Exists(self) &&
            string.Equals(Path.GetFileName(Path.GetDirectoryName(root)), "versions",
                StringComparison.OrdinalIgnoreCase))
        {
            return (self, true);
        }

        var versions = Path.Combine(root, "versions");
        if (!Directory.Exists(versions))
        {
            return null;
        }

        var candidate = Directory.EnumerateDirectories(versions)
            .Select(dir => Path.Combine(dir, $"{Path.GetFileName(dir)}.json"))
            .Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        return candidate is null ? null : (candidate, false);
    }

    private static (LoaderKind Loader, string? Version) DetectLoader(string id, string? inheritsFrom)
    {
        if (id.StartsWith("fabric-loader-", StringComparison.OrdinalIgnoreCase))
        {
            var rest = id["fabric-loader-".Length..];
            var game = inheritsFrom ?? string.Empty;
            if (game.Length > 0 && rest.EndsWith("-" + game, StringComparison.OrdinalIgnoreCase))
            {
                return (LoaderKind.Fabric, rest[..^(game.Length + 1)]);
            }

            var split = rest.LastIndexOf('-');
            return split > 0 ? (LoaderKind.Fabric, rest[..split]) : (LoaderKind.Fabric, null);
        }

        var neoIndex = id.IndexOf("-neoforge-", StringComparison.OrdinalIgnoreCase);
        if (neoIndex >= 0)
        {
            return (LoaderKind.NeoForge, id[(neoIndex + "-neoforge-".Length)..]);
        }

        var forgeIndex = id.IndexOf("-forge-", StringComparison.OrdinalIgnoreCase);
        if (forgeIndex >= 0)
        {
            return (LoaderKind.Forge, id[(forgeIndex + "-forge-".Length)..]);
        }

        var legacyForge = id.IndexOf("-forge", StringComparison.OrdinalIgnoreCase);
        if (legacyForge >= 0 && inheritsFrom is not null)
        {
            var tail = id[(legacyForge + "-forge".Length)..];
            if (tail.StartsWith(inheritsFrom + "-", StringComparison.OrdinalIgnoreCase))
            {
                return (LoaderKind.Forge, tail[(inheritsFrom.Length + 1)..]);
            }
        }

        return (LoaderKind.Vanilla, null);
    }

    private static IEnumerable<string> EnumerateGameFiles(string gameDir)
    {
        if (!Directory.Exists(gameDir))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(gameDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(gameDir, file);
            var first = relative.Split(Path.DirectorySeparatorChar, '/')[0];
            if (CopyExclusions.Contains(first, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return file;
        }
    }

    private static string BuildInstanceId(string name)
    {
        var slug = new string(name.Select(c =>
            char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '-').ToArray()).Trim('-');
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = "imported";
        }

        if (slug.Length > 32)
        {
            slug = slug[..32];
        }

        var suffix = Guid.NewGuid().ToString("N")[..6];
        return PathGuard.ValidateId($"{slug}-{suffix}", nameof(name));
    }

    private static string? ReadMultiMcName(string root)
    {
        var cfg = Path.Combine(root, "instance.cfg");
        if (!File.Exists(cfg))
        {
            return null;
        }

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
        names.Select(name => Path.Combine(root, name))
            .FirstOrDefault(Directory.Exists);

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long SafeLength(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch (IOException)
        {
            return 0;
        }
    }
}
