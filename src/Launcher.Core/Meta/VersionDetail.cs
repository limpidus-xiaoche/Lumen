using System.Text.Json;
using System.Text.Json.Serialization;

namespace Launcher.Core.Meta;

public sealed class VersionDetail
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("inheritsFrom")] public string? InheritsFrom { get; set; }
    [JsonPropertyName("mainClass")] public string? MainClass { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("assets")] public string? Assets { get; set; }
    [JsonPropertyName("assetIndex")] public AssetIndexRef? AssetIndex { get; set; }
    [JsonPropertyName("downloads")] public VersionDownloads? Downloads { get; set; }
    [JsonPropertyName("javaVersion")] public JavaVersionRef? JavaVersion { get; set; }
    [JsonPropertyName("libraries")] public List<Library> Libraries { get; set; } = [];
    [JsonPropertyName("arguments")] public VersionArguments? Arguments { get; set; }
    [JsonPropertyName("minecraftArguments")] public string? MinecraftArguments { get; set; }
    [JsonPropertyName("releaseTime")]
    [JsonConverter(typeof(TolerantDateTimeOffsetConverter))]
    public DateTimeOffset? ReleaseTime { get; set; }
}

/// <summary>
/// 加载器 JSON 可能出现空串或非法时间（例如 Fabric profile 的 releaseTime: ""），统一按 null 处理。
/// </summary>
internal sealed class TolerantDateTimeOffsetConverter : JsonConverter<DateTimeOffset?>
{
    public override DateTimeOffset? Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            reader.Skip();
            return null;
        }

        var text = reader.GetString();
        return DateTimeOffset.TryParse(text, out var value) ? value : null;
    }

    public override void Write(
        Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value);
    }
}

public sealed class AssetIndexRef
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("sha1")] public string? Sha1 { get; set; }
    [JsonPropertyName("size")] public long Size { get; set; }
    [JsonPropertyName("totalSize")] public long TotalSize { get; set; }
    [JsonPropertyName("url")] public string? Url { get; set; }
    [JsonPropertyName("virtual")] public bool Virtual { get; set; }
    [JsonPropertyName("map_to_resources")] public bool MapToResources { get; set; }
}

public sealed class VersionDownloads
{
    [JsonPropertyName("client")] public DownloadArtifact? Client { get; set; }
    [JsonPropertyName("server")] public DownloadArtifact? Server { get; set; }
}

public sealed class DownloadArtifact
{
    [JsonPropertyName("path")] public string? Path { get; set; }
    [JsonPropertyName("sha1")] public string? Sha1 { get; set; }
    [JsonPropertyName("size")] public long Size { get; set; }
    [JsonPropertyName("url")] public string? Url { get; set; }
}

public sealed class JavaVersionRef
{
    [JsonPropertyName("component")] public string? Component { get; set; }
    [JsonPropertyName("majorVersion")] public int MajorVersion { get; set; }
}

public sealed class Library
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    /// <summary>加载器 profile JSON 使用的仓库根地址（如 https://maven.fabricmc.net/）。</summary>
    [JsonPropertyName("url")] public string? Url { get; set; }

    [JsonPropertyName("sha1")] public string? Sha1 { get; set; }
    [JsonPropertyName("size")] public long Size { get; set; }
    [JsonPropertyName("downloads")] public LibraryDownloads? Downloads { get; set; }
    [JsonPropertyName("rules")] public List<Rule>? Rules { get; set; }
    [JsonPropertyName("natives")] public Dictionary<string, string>? Natives { get; set; }
    [JsonPropertyName("extract")] public Extract? Extract { get; set; }

    public string DedupeKey
    {
        get
        {
            var parts = Name.Split(':');
            if (parts.Length < 3)
            {
                return Name;
            }

            var classifier = parts.Length >= 4 ? parts[3] : string.Empty;
            return string.IsNullOrEmpty(classifier)
                ? $"{parts[0]}:{parts[1]}"
                : $"{parts[0]}:{parts[1]}:{classifier}";
        }
    }
}

public sealed class LibraryDownloads
{
    [JsonPropertyName("artifact")] public DownloadArtifact? Artifact { get; set; }
    [JsonPropertyName("classifiers")] public Dictionary<string, DownloadArtifact>? Classifiers { get; set; }
}

public sealed class Extract
{
    [JsonPropertyName("exclude")] public List<string>? Exclude { get; set; }
}

public sealed class Rule
{
    [JsonPropertyName("action")] public string Action { get; set; } = "allow";
    [JsonPropertyName("os")] public OsRule? Os { get; set; }
    [JsonPropertyName("features")] public Dictionary<string, bool>? Features { get; set; }
}

public sealed class OsRule
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("arch")] public string? Arch { get; set; }
    [JsonPropertyName("version")] public string? Version { get; set; }
}

public sealed class VersionArguments
{
    [JsonPropertyName("game")] public List<JsonElement> Game { get; set; } = [];
    [JsonPropertyName("jvm")] public List<JsonElement> Jvm { get; set; } = [];
}
