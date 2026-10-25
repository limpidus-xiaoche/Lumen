using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using Launcher.Protocol;

namespace Launcher.Core.Instances;

/// <summary>
/// 扫描 mods 目录：`*.jar` 为启用，`*.jar.disabled` 为禁用。元数据读取失败时回退文件名。
/// </summary>
public static partial class ModScanner
{
    private const string FabricMetadata = "fabric.mod.json";
    private const string ForgeMetadata = "META-INF/mods.toml";
    private const string NeoForgeMetadata = "META-INF/neoforge.mods.toml";

    public static IReadOnlyList<ModEntry> Scan(string gameDir, LoaderKind loader)
    {
        var modsDir = Path.Combine(gameDir, "mods");
        if (!Directory.Exists(modsDir))
        {
            return [];
        }

        var result = new List<ModEntry>();
        foreach (var file in Directory.EnumerateFiles(modsDir, "*", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(file);
            var enabled = name.EndsWith(".jar", StringComparison.OrdinalIgnoreCase);
            if (!enabled && !name.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var stem = EnabledStem(name);
            var metadata = ReadMetadata(file);
            var id = metadata.Id ?? stem;
            result.Add(new ModEntry(
                id,
                metadata.Name ?? id,
                metadata.Version,
                name,
                enabled,
                loader,
                metadata.Dependencies,
                IconPath: null));
        }

        return result
            .OrderByDescending(m => m.Enabled)
            .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool SetEnabled(string gameDir, string modId, bool enabled)
    {
        var modsDir = Path.Combine(gameDir, "mods");
        if (!Directory.Exists(modsDir))
        {
            return false;
        }

        var entry = Scan(gameDir, LoaderKind.Vanilla)
            .FirstOrDefault(m => string.Equals(m.Id, modId, StringComparison.OrdinalIgnoreCase)
                                 || string.Equals(EnabledStem(m.FileName), modId, StringComparison.OrdinalIgnoreCase));
        if (entry is null || entry.Enabled == enabled)
        {
            return entry is not null;
        }

        var source = Path.Combine(modsDir, entry.FileName);
        var target = enabled
            ? source[..^".disabled".Length]
            : source + ".disabled";
        File.Move(source, target, overwrite: false);
        return true;
    }

    private static string EnabledStem(string fileName) =>
        fileName.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase)
            ? fileName[..^".jar.disabled".Length]
            : fileName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)
                ? fileName[..^".jar".Length]
                : Path.GetFileNameWithoutExtension(fileName);

    private static ModMetadata ReadMetadata(string filePath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(filePath);

            var fabric = archive.GetEntry(FabricMetadata);
            if (fabric is not null)
            {
                return ReadFabric(fabric);
            }

            var forge = archive.GetEntry(NeoForgeMetadata) ?? archive.GetEntry(ForgeMetadata);
            if (forge is not null)
            {
                return ReadForge(forge);
            }
        }
        catch (InvalidDataException)
        {
            // 不是有效 jar，回退文件名。
        }
        catch (IOException)
        {
            // 文件被占用等，回退文件名。
        }

        return new ModMetadata(null, null, null, []);
    }

    private static ModMetadata ReadFabric(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;

        var id = root.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
        var name = root.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
        var version = root.TryGetProperty("version", out var versionElement) ? versionElement.GetString() : null;

        var dependencies = new List<string>();
        if (root.TryGetProperty("depends", out var depends) &&
            depends.ValueKind == JsonValueKind.Object)
        {
            foreach (var dependency in depends.EnumerateObject())
            {
                dependencies.Add(dependency.Name);
            }
        }

        return new ModMetadata(
            id,
            name ?? id,
            version,
            dependencies.Where(d => !string.Equals(d, "minecraft", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private static ModMetadata ReadForge(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open());
        var text = reader.ReadToEnd();

        var modId = ModIdRegex().Match(text) is { Success: true } modMatch ? modMatch.Groups[1].Value : null;
        var displayName = DisplayNameRegex().Match(text) is { Success: true } nameMatch
            ? nameMatch.Groups[1].Value
            : null;
        var version = VersionRegex().Match(text) is { Success: true } versionMatch
            ? versionMatch.Groups[1].Value
            : null;

        if (version is not null && version.Contains("${", StringComparison.Ordinal))
        {
            version = null;
        }

        return new ModMetadata(
            modId,
            displayName ?? modId,
            version,
            Array.Empty<string>());
    }

    [GeneratedRegex("modId\\s*=\\s*\"([^\"]+)\"", RegexOptions.Multiline)]
    private static partial Regex ModIdRegex();

    [GeneratedRegex("displayName\\s*=\\s*\"([^\"]+)\"", RegexOptions.Multiline)]
    private static partial Regex DisplayNameRegex();

    [GeneratedRegex("(?:^|\\n)\\s*version\\s*=\\s*\"([^\"]+)\"", RegexOptions.Multiline)]
    private static partial Regex VersionRegex();

    private sealed record ModMetadata(
        string? Id, string? Name, string? Version, IReadOnlyList<string> Dependencies);
}
