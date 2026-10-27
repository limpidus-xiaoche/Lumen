using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Launcher.Core.Java;
using Launcher.Core.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Launcher.Core.Diagnostics;

/// <summary>
/// 诊断包（§6.8）：日志（脱敏）+ 版本/系统信息 + Java 列表 + 设置（脱敏）+ 实例配置。
/// 显式排除 accounts.dat / args.txt / .part.meta 与任何 token 字段。
/// </summary>
public sealed class DiagnosticsExporter
{
    private static readonly Regex SecretPattern = new(
        @"(?i)\b(access[_-]?token|refresh[_-]?token|client[_-]?token|session[_-]?token|password|authorization)\b(\s*[:=]\s*)(""[^""]*""|[^\s,;}]+)",
        RegexOptions.Compiled);

    private static readonly Regex BearerPattern = new(
        @"(?i)\bBearer\s+[A-Za-z0-9._~+/=-]+",
        RegexOptions.Compiled);

    private static readonly Regex UrlCredentialPattern = new(
        @"//[^/@\s]+@",
        RegexOptions.Compiled);

    private readonly LauncherPaths _paths;
    private readonly AppSettingsStore _settingsStore;
    private readonly SqliteStore _sqlite;
    private readonly JavaLocator? _javaLocator;
    private readonly ILogger<DiagnosticsExporter> _logger;

    public DiagnosticsExporter(
        LauncherPaths paths,
        AppSettingsStore settingsStore,
        SqliteStore sqlite,
        JavaLocator? javaLocator = null,
        ILogger<DiagnosticsExporter>? logger = null)
    {
        _paths = paths;
        _settingsStore = settingsStore;
        _sqlite = sqlite;
        _javaLocator = javaLocator;
        _logger = logger ?? NullLogger<DiagnosticsExporter>.Instance;
    }

    public async Task<string> ExportAsync(string? instanceId, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_paths.ExportsDir);
        var output = Path.Combine(
            _paths.ExportsDir,
            $"diagnostics-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.zip");

        var instances = await _sqlite.GetInstancesAsync(ct);
        using var archive = ZipFile.Open(output, ZipArchiveMode.Create);

        AddText(archive, "README.txt", ReadmeText());

        var version = new DiagnosticsVersionInfo(
            AppVersion: typeof(DiagnosticsExporter).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            EntryVersion: Environment.Version.ToString(),
            OsDescription: RuntimeInformation.OSDescription,
            OsVersion: Environment.OSVersion.VersionString,
            Architecture: RuntimeInformation.ProcessArchitecture.ToString(),
            Framework: RuntimeInformation.FrameworkDescription,
            Portable: _paths.IsPortable,
            PortableFellBack: _paths.PortableFellBack,
            ExportedAt: DateTimeOffset.Now);
        AddJson(archive, "version.json", version);

        AddJson(archive, "system.json", new DiagnosticsSystemInfo(
            RuntimeInformation.OSDescription,
            RuntimeInformation.ProcessArchitecture.ToString(),
            RuntimeInformation.OSArchitecture.ToString(),
            Environment.ProcessorCount,
            GetTotalMemoryMb(),
            GetFreeDiskMb(_paths.LocalRoot)));

        var settingsJson = JsonSerializer.Serialize(
            _settingsStore.Load(), SettingsJsonContext.Default.AppSettings);
        AddText(archive, "settings.json", Redact(settingsJson));

        var javas = _javaLocator?.Detect() ?? Array.Empty<Launcher.Protocol.JavaRuntime>();
        AddJson(archive, "java.json", javas
            .Select(j => new DiagnosticsJavaInfo(
                RedactPath(j.Path), j.MajorVersion, j.Is64Bit, j.Vendor, j.VersionString))
            .ToArray());
        AddJson(archive, "instances.json", instances
            .Select(i => new DiagnosticsInstanceInfo(
                i.Id, i.Name, i.VersionId, i.Loader.ToString(), i.LoaderVersion,
                i.Isolation.ToString(), i.State.ToString()))
            .ToArray());

        AddLogFiles(archive);

        if (!string.IsNullOrWhiteSpace(instanceId))
        {
            await AddInstanceFilesAsync(archive, instanceId, instances, ct);
        }

        _logger.LogInformation("诊断包已导出：{Path}", output);
        return output;
    }

    private void AddLogFiles(ZipArchive archive)
    {
        if (!Directory.Exists(_paths.LogsDir))
        {
            return;
        }

        foreach (var log in Directory.GetFiles(_paths.LogsDir, "*.log"))
        {
            try
            {
                var text = File.ReadAllText(log);
                AddText(archive, $"logs/{Path.GetFileName(log)}", Redact(text));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogDebug(ex, "跳过日志文件：{Log}", log);
            }
        }
    }

    private async Task AddInstanceFilesAsync(
        ZipArchive archive, string instanceId, IReadOnlyList<Launcher.Protocol.InstanceSummary> instances,
        CancellationToken ct)
    {
        var instance = instances.FirstOrDefault(i => i.Id == instanceId);
        if (instance is null)
        {
            return;
        }

        var root = await _sqlite.GetRootPathAsync(instanceId, ct);
        var gameDir = !string.IsNullOrWhiteSpace(root)
            ? root
            : instance.Isolation == Launcher.Protocol.IsolationLevel.None
                ? Path.Combine(_paths.SharedDir, "game")
                : Path.Combine(_paths.InstancesDir, instanceId);

        var options = Path.Combine(gameDir, "options.txt");
        if (File.Exists(options))
        {
            AddText(archive, $"instance/options.txt", Redact(File.ReadAllText(options)));
        }
    }

    private static void AddJson(ZipArchive archive, string name, DiagnosticsVersionInfo value) =>
        AddText(archive, name,
            JsonSerializer.Serialize(value, DiagnosticsJsonContext.Default.DiagnosticsVersionInfo));

    private static void AddJson(ZipArchive archive, string name, DiagnosticsSystemInfo value) =>
        AddText(archive, name,
            JsonSerializer.Serialize(value, DiagnosticsJsonContext.Default.DiagnosticsSystemInfo));

    private static void AddJson(ZipArchive archive, string name, DiagnosticsJavaInfo[] value) =>
        AddText(archive, name,
            JsonSerializer.Serialize(value, DiagnosticsJsonContext.Default.DiagnosticsJavaInfoArray));

    private static void AddJson(ZipArchive archive, string name, DiagnosticsInstanceInfo[] value) =>
        AddText(archive, name,
            JsonSerializer.Serialize(value, DiagnosticsJsonContext.Default.DiagnosticsInstanceInfoArray));

    private static void AddText(ZipArchive archive, string name, string text)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(text);
    }

    private static string ReadmeText() =>
        """
        Lumen 诊断包
        ============

        内容：启动器/系统信息、Java 列表、脱敏后的设置与日志、实例摘要。
        已排除：accounts.dat、args.txt、.part.meta 以及所有 token / 密码字段。

        日志中的 accessToken / refreshToken / password / Authorization 已替换为 ***，
        用户目录路径已替换为 %USERPROFILE%，可直接附在 issue 中。
        """;

    internal static string Redact(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile))
        {
            text = text.Replace(userProfile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        }

        text = BearerPattern.Replace(text, "Bearer ***");
        text = SecretPattern.Replace(text, "$1$2***");
        text = UrlCredentialPattern.Replace(text, "//***@");
        return text;
    }

    private static string RedactPath(string path) => Redact(path);

    private static long GetTotalMemoryMb()
    {
        try
        {
            return GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024);
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or NotSupportedException)
        {
            return 0;
        }
    }

    private static long GetFreeDiskMb(string path)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrEmpty(root))
            {
                return 0;
            }

            return new DriveInfo(root).AvailableFreeSpace / (1024 * 1024);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException)
        {
            return 0;
        }
    }
}

public sealed record DiagnosticsVersionInfo(
    string AppVersion,
    string EntryVersion,
    string OsDescription,
    string OsVersion,
    string Architecture,
    string Framework,
    bool Portable,
    bool PortableFellBack,
    DateTimeOffset ExportedAt);

public sealed record DiagnosticsSystemInfo(
    string OsDescription,
    string ProcessArchitecture,
    string OsArchitecture,
    int ProcessorCount,
    long TotalMemoryMb,
    long FreeDiskMb);

public sealed record DiagnosticsJavaInfo(
    string Path,
    int MajorVersion,
    bool Is64Bit,
    string Vendor,
    string? VersionString);

public sealed record DiagnosticsInstanceInfo(
    string Id,
    string Name,
    string VersionId,
    string Loader,
    string? LoaderVersion,
    string Isolation,
    string State);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(DiagnosticsVersionInfo))]
[JsonSerializable(typeof(DiagnosticsSystemInfo))]
[JsonSerializable(typeof(DiagnosticsJavaInfo[]))]
[JsonSerializable(typeof(DiagnosticsInstanceInfo[]))]
internal sealed partial class DiagnosticsJsonContext : JsonSerializerContext;
