using System.Text.Json;
using System.Text.Json.Serialization;

namespace Launcher.Core.Update;

/// <summary>
/// 更新清单（releases.&lt;channel&gt;.json，§12.1）。字段固定
/// version / minSupported / url / sha256 / size / publishedAt / notes，忽略未知字段。
/// </summary>
public sealed record ReleaseManifest(
    string Version,
    string? MinSupported,
    string Url,
    long Size,
    string Sha256,
    string? Notes,
    DateTimeOffset? PublishedAt);

public static class ReleaseManifestParser
{
    public static ReleaseManifest Parse(string json)
    {
        ReleaseManifestDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize(json, UpdateJsonContext.Default.ReleaseManifestDto);
        }
        catch (JsonException ex)
        {
            throw new UpdateException("更新清单 JSON 解析失败。", ex);
        }

        if (dto is null)
        {
            throw new UpdateException("更新清单为空。");
        }

        if (string.IsNullOrWhiteSpace(dto.Version))
        {
            throw new UpdateException("更新清单缺少 version。");
        }

        if (string.IsNullOrWhiteSpace(dto.Url))
        {
            throw new UpdateException("更新清单缺少 url。");
        }

        if (string.IsNullOrWhiteSpace(dto.Sha256) || !IntegrityManifest.IsSha256Hex(dto.Sha256))
        {
            throw new UpdateException("更新清单缺少合法的 sha256。");
        }

        if (dto.Size <= 0)
        {
            throw new UpdateException("更新清单缺少合法的 size。");
        }

        return new ReleaseManifest(
            dto.Version.Trim(),
            string.IsNullOrWhiteSpace(dto.MinSupported) ? null : dto.MinSupported.Trim(),
            dto.Url.Trim(),
            dto.Size,
            dto.Sha256!.ToLowerInvariant(),
            dto.Notes,
            dto.PublishedAt);
    }
}

/// <summary>版本号比较：数字段逐段比较，预发布后缀低于同号正式版。</summary>
public static class AppVersion
{
    public static int Compare(string? left, string? right)
    {
        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var a = Parse(left);
        var b = Parse(right);
        var length = Math.Max(a.Numbers.Length, b.Numbers.Length);
        for (var i = 0; i < length; i++)
        {
            var x = i < a.Numbers.Length ? a.Numbers[i] : 0;
            var y = i < b.Numbers.Length ? b.Numbers[i] : 0;
            if (x != y)
            {
                return x.CompareTo(y);
            }
        }

        // 正式版 > 预发布版
        if (a.Prerelease is null && b.Prerelease is not null)
        {
            return 1;
        }

        if (a.Prerelease is not null && b.Prerelease is null)
        {
            return -1;
        }

        return string.Compare(a.Prerelease, b.Prerelease, StringComparison.OrdinalIgnoreCase);
    }

    private static (int[] Numbers, string? Prerelease) Parse(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return (new[] { 0 }, null);
        }

        var text = version.Trim().TrimStart('v', 'V');
        var separator = text.IndexOfAny(['-', '+']);
        var core = separator >= 0 ? text[..separator] : text;
        var prerelease = separator >= 0 && text[separator] == '-' ? text[(separator + 1)..] : null;

        var numbers = core
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => int.TryParse(part, out var value) ? value : 0)
            .ToArray();
        return (numbers.Length == 0 ? new[] { 0 } : numbers, prerelease);
    }
}

internal sealed class ReleaseManifestDto
{
    public string? Version { get; set; }
    public string? MinSupported { get; set; }
    public string? Url { get; set; }
    public long Size { get; set; }
    public string? Sha256 { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ReleaseManifestDto))]
internal sealed partial class UpdateJsonContext : JsonSerializerContext;
