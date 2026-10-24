using Launcher.Core.Storage;
using Launcher.Protocol;
using Xunit;

namespace Launcher.Core.Tests;

public sealed class StorageTests
{
    [Fact]
    public void Settings_RoundTrip()
    {
        var root = Path.Combine(Path.GetTempPath(), "lumen-tests", Guid.NewGuid().ToString("N"));
        var paths = new LauncherPaths(Path.Combine(root, "config"), Path.Combine(root, "local"));
        paths.EnsureCreated();

        var store = new AppSettingsStore(paths);
        var defaultSettings = store.Load();
        Assert.False(string.IsNullOrWhiteSpace(defaultSettings.GameDir));

        store.Save(defaultSettings with { MaxMemoryMb = 6144, AccentColor = "#FF8800" });
        var reloaded = store.Load();

        Assert.Equal(6144, reloaded.MaxMemoryMb);
        Assert.Equal("#FF8800", reloaded.AccentColor);

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task Sqlite_Schema_Is_Version3()
    {
        var root = Path.Combine(Path.GetTempPath(), "lumen-tests", Guid.NewGuid().ToString("N"));
        var paths = new LauncherPaths(Path.Combine(root, "config"), Path.Combine(root, "local"));
        paths.EnsureCreated();

        var store = new SqliteStore(paths);
        await store.InitializeAsync();

        Assert.Equal(3, await store.GetSchemaVersionAsync());
        Assert.Empty(await store.GetInstancesAsync());

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(root, recursive: true);
    }
}
