using Launcher.Protocol;

namespace Launcher.Core.Meta;

public sealed class VersionResolver
{
    private readonly ManifestClient _client;
    private readonly LoaderMetaClient? _loaderMeta;

    public VersionResolver(ManifestClient client, LoaderMetaClient? loaderMeta = null)
    {
        _client = client;
        _loaderMeta = loaderMeta;
    }

    public async Task<VersionDetail> ResolveAsync(string versionId, CancellationToken ct = default)
    {
        var manifest = await _client.GetManifestAsync(force: false, ct);
        return await ResolveAsync(versionId, manifest, new HashSet<string>(StringComparer.OrdinalIgnoreCase), ct);
    }

    /// <summary>解析「原版 + 加载器」合并后的版本：原版作为父版本，加载器 profile 作为子版本。</summary>
    public async Task<VersionDetail> ResolveAsync(
        string versionId, LoaderKind loader, string? loaderVersion, CancellationToken ct = default)
    {
        var vanilla = await ResolveAsync(versionId, ct);
        if (loader == LoaderKind.Vanilla || string.IsNullOrWhiteSpace(loaderVersion))
        {
            return vanilla;
        }

        if (_loaderMeta is null)
        {
            throw new NotSupportedException($"{loader} 需要加载器元数据客户端，当前未注入");
        }

        var profile = await _loaderMeta.GetProfileAsync(loader, versionId, loaderVersion, ct);
        return Merge(vanilla, profile);
    }

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
        VersionManifest manifest,
        HashSet<string> visiting,
        CancellationToken ct)
    {
        if (!visiting.Add(versionId))
        {
            throw new InvalidOperationException($"版本继承出现循环：{versionId}");
        }

        var summary = manifest.Versions.FirstOrDefault(v => v.Id == versionId)
            ?? throw new InvalidOperationException($"清单中不存在版本 {versionId}");
        var child = await _client.GetVersionDetailAsync(summary, ct);

        if (string.IsNullOrWhiteSpace(child.InheritsFrom))
        {
            return child;
        }

        var parent = await ResolveAsync(child.InheritsFrom, manifest, visiting, ct);
        return Merge(parent, child);
    }
}
