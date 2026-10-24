using Launcher.Core.Storage;
using Launcher.Protocol;

namespace Launcher.Core.Meta;

public sealed class VersionResolver
{
    private readonly ManifestClient _client;
    private readonly LauncherPaths? _paths;

    public VersionResolver(ManifestClient client, LauncherPaths? paths = null)
    {
        _client = client;
        _paths = paths;
    }

    public Task<VersionDetail> ResolveAsync(string versionId, CancellationToken ct = default) =>
        ResolveAsync(versionId, manifest: null, new HashSet<string>(StringComparer.OrdinalIgnoreCase), ct);

    public static VersionDetail Merge(VersionDetail parent, VersionDetail child)
    {
        var libraries = new List<Library>(parent.Libraries);
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < libraries.Count; i++)
        {
            index[libraries[i].DedupeKey] = i;
        }

        foreach (var library in child.Libraries)
        {
            if (index.TryGetValue(library.DedupeKey, out var existing))
            {
                libraries[existing] = library;
            }
            else
            {
                index[library.DedupeKey] = libraries.Count;
                libraries.Add(library);
            }
        }

        var parentArguments = parent.Arguments ?? new VersionArguments();
        var childArguments = child.Arguments ?? new VersionArguments();

        return new VersionDetail
        {
            Id = child.Id,
            InheritsFrom = null,
            MainClass = child.MainClass ?? parent.MainClass,
            Type = child.Type ?? parent.Type,
            Assets = child.Assets ?? parent.Assets,
            AssetIndex = child.AssetIndex ?? parent.AssetIndex,
            Downloads = child.Downloads ?? parent.Downloads,
            JavaVersion = child.JavaVersion ?? parent.JavaVersion,
            Libraries = libraries,
            Arguments = new VersionArguments
            {
                Game = [.. parentArguments.Game, .. childArguments.Game],
                Jvm = [.. parentArguments.Jvm, .. childArguments.Jvm],
            },
            MinecraftArguments = child.MinecraftArguments ?? parent.MinecraftArguments,
            ReleaseTime = child.ReleaseTime ?? parent.ReleaseTime,
        };
    }

    private async Task<VersionDetail> ResolveAsync(
        string versionId,
        VersionManifest? manifest,
        HashSet<string> visiting,
        CancellationToken ct)
    {
        if (!visiting.Add(versionId))
        {
            throw new InvalidOperationException($"版本继承出现循环：{versionId}");
        }

        // 加载器子版本（Fabric/Forge/NeoForge）只存在于本地 versions 目录。
        var child = TryLoadLocal(versionId);
        if (child is null)
        {
            manifest ??= await _client.GetManifestAsync(force: false, ct);
            var summary = manifest.Versions.FirstOrDefault(v => v.Id == versionId)
                ?? throw new InvalidOperationException($"清单中不存在版本 {versionId}");
            child = await _client.GetVersionDetailAsync(summary, ct);
        }

        if (string.IsNullOrWhiteSpace(child.InheritsFrom))
        {
            return child;
        }

        var parent = await ResolveAsync(child.InheritsFrom, manifest, visiting, ct);
        return Merge(parent, child);
    }

    private VersionDetail? TryLoadLocal(string versionId)
    {
        if (_paths is null)
        {
            return null;
        }

        var local = Path.Combine(_paths.SharedDir, "versions", versionId, $"{versionId}.json");
        return File.Exists(local) ? ManifestClient.DeserializeDetail(File.ReadAllText(local)) : null;
    }
}
