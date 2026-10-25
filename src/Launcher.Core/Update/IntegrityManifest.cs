using System.Text.Json;
using System.Text.Json.Serialization;

namespace Launcher.Core.Update;

public sealed record IntegrityEntry(string Path, long Size, string Sha256);

/// <summary>
/// 随包发布的 SHA-256 清单（§12.1）。字段固定 version/algorithm/generatedAt/files，
/// 解析忽略未知字段，为 v2 的 signature/keyId 留位。
/// </summary>
public sealed record IntegrityManifest(
    string Version,
    string Algorithm,
    DateTimeOffset? GeneratedAt,
    IReadOnlyList<IntegrityEntry> Files)
{
    public static IntegrityManifest Parse(string json)
    {
        IntegrityManifestDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize(json, IntegrityJsonContext.Default.IntegrityManifestDto);
        }
        catch (JsonException ex)
        {
            throw new IntegrityException("完整性清单 JSON 解析失败。", inner: ex);
        }

        if (dto is null || string.IsNullOrWhiteSpace(dto.Version))
        {
            throw new IntegrityException("完整性清单缺少 version 字段。");
        }

        var algorithm = string.IsNullOrWhiteSpace(dto.Algorithm) ? "sha256" : dto.Algorithm.Trim();
        if (!string.Equals(algorithm, "sha256", StringComparison.OrdinalIgnoreCase))
        {
            throw new IntegrityException($"不支持的完整性算法：{algorithm}");
        }

        if (dto.Files is null || dto.Files.Count == 0)
        {
            throw new IntegrityException("完整性清单没有文件条目。");
        }

        var files = new List<IntegrityEntry>(dto.Files.Count);
        foreach (var item in dto.Files)
        {
            var relative = NormalizeRelativePath(item.Path);
            if (item.Size < 0)
            {
                throw new IntegrityException($"清单条目 size 非法：{relative}");
            }

            if (!IsSha256Hex(item.Sha256))
            {
                throw new IntegrityException($"清单条目 sha256 非法：{relative}");
            }

            files.Add(new IntegrityEntry(relative, item.Size, item.Sha256!.ToLowerInvariant()));
        }

        return new IntegrityManifest(dto.Version.Trim(), algorithm.ToLowerInvariant(), dto.GeneratedAt, files);
    }

    internal static string NormalizeRelativePath(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new IntegrityException("清单条目缺少 path。");
        }

        var normalized = raw.Replace('\\', '/').Trim();
        var rooted = Path.IsPathRooted(normalized) || normalized.Contains(':') ||
                     normalized.StartsWith("//", StringComparison.Ordinal);
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (rooted || segments.Length == 0 || segments.Any(s => s == ".." || s == "."))
        {
            throw new IntegrityException($"清单条目 path 非法：{raw}");
        }

        return string.Join('/', segments);
    }

    internal static bool IsSha256Hex(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);
}

internal sealed class IntegrityManifestDto
{
    public string? Version { get; set; }
    public string? Algorithm { get; set; }
    public DateTimeOffset? GeneratedAt { get; set; }
    public List<IntegrityEntryDto>? Files { get; set; }
}

internal sealed class IntegrityEntryDto
{
    public string? Path { get; set; }
    public long Size { get; set; }
    public string? Sha256 { get; set; }
}

internal sealed class IntegrityCacheEntryDto
{
    public string? Version { get; set; }
    public long MaxMtimeUtcTicks { get; set; }
    public long TotalSize { get; set; }
    public string? Mode { get; set; }
    public DateTimeOffset CheckedAt { get; set; }
    public int CheckedFiles { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(IntegrityManifestDto))]
[JsonSerializable(typeof(IntegrityCacheEntryDto))]
internal sealed partial class IntegrityJsonContext : JsonSerializerContext;
