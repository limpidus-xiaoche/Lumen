using System.Text.Json;
using System.Text.Json.Serialization;
using Launcher.Protocol;

namespace Launcher.Core.Storage;

public sealed class AppSettingsStore
{
    private readonly LauncherPaths _paths;
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public AppSettingsStore(LauncherPaths paths)
    {
        _paths = paths;
    }

    public AppSettings Load()
    {
        var json = AtomicFile.ReadAllTextOrEmpty(_paths.ConfigFile);
        if (string.IsNullOrWhiteSpace(json))
        {
            return CreateDefault();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings);
            return parsed ?? CreateDefault();
        }
        catch (JsonException)
        {
            return CreateDefault();
        }
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, SettingsJsonContext.Default.AppSettings);
        AtomicFile.WriteAllText(_paths.ConfigFile, json);
    }

    private AppSettings CreateDefault() => new()
    {
        GameDir = Path.Combine(_paths.SharedDir, "game"),
    };
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
public sealed partial class SettingsJsonContext : JsonSerializerContext;
