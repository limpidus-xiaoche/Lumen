using System.Security.Cryptography;
using System.Text.Json;
using Launcher.Core.Storage;
using Launcher.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Launcher.Core.Update;

/// <summary>
/// 自身完整性校验（§12.1）。快校验只覆盖入口可执行文件并按「版本 + mtime」缓存；
/// 全量校验逐文件比对 SHA-256。任何不符即 fail-closed，抛 E8002。
/// </summary>
public sealed class IntegrityService
{
    private const string CacheFileName = "integrity-check.json";

    private readonly LauncherPaths _paths;
    private readonly ILogger<IntegrityService> _logger;
    private readonly string _appDir;
    private readonly bool _requireManifest;

    public IntegrityService(
        LauncherPaths paths,
        ILogger<IntegrityService>? logger = null,
        string? appDir = null,
        bool? requireManifest = null)
    {
        _paths = paths;
        _logger = logger ?? NullLogger<IntegrityService>.Instance;
        _appDir = Path.GetFullPath(appDir ?? AppContext.BaseDirectory);
        _requireManifest = requireManifest ?? DefaultRequireManifest;
    }

#if DEBUG
    private const bool DefaultRequireManifest = false;
#else
    private const bool DefaultRequireManifest = true;
#endif

    public string ManifestPath => Path.Combine(_appDir, "integrity.json");

    public string CachePath => Path.Combine(_paths.CacheDir, CacheFileName);

    public async Task<IntegrityReport> VerifyAsync(
        IntegrityCheckMode mode, bool force = false, CancellationToken ct = default)
    {
        if (!File.Exists(ManifestPath))
        {
            if (_requireManifest)
            {
                throw new IntegrityException($"完整性清单缺失：{ManifestPath}");
            }

            _logger.LogWarning("完整性清单缺失，已跳过校验（开发构建）：{Path}", ManifestPath);
            return new IntegrityReport(
                true, mode, 0, Array.Empty<string>(),
                "完整性清单缺失，已跳过（开发构建）。", DateTimeOffset.Now);
        }

        IntegrityManifest manifest;
        try
        {
            var json = await File.ReadAllTextAsync(ManifestPath, ct);
            manifest = IntegrityManifest.Parse(json);
        }
        catch (IntegrityException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new IntegrityException("完整性清单读取失败。", inner: ex);
        }

        var targets = SelectTargets(manifest, mode);
        var signature = ComputeSignature(targets);
        if (mode == IntegrityCheckMode.Quick && !force &&
            TryReadCache(manifest, signature, mode, out var cached))
        {
            _logger.LogDebug("完整性快校验命中缓存：{Version}", manifest.Version);
            return cached;
        }

        var failures = new List<string>();
        foreach (var entry in targets)
        {
            ct.ThrowIfCancellationRequested();
            var fullPath = ResolveEntryPath(entry.Path);
            var info = new FileInfo(fullPath);
            if (!info.Exists)
            {
                failures.Add($"{entry.Path}（缺失）");
                continue;
            }

            if (info.Length != entry.Size)
            {
                failures.Add($"{entry.Path}（size {info.Length} != {entry.Size}）");
                continue;
            }

            string actual;
            await using (var stream = info.OpenRead())
            {
                actual = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct));
            }

            if (!string.Equals(actual, entry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"{entry.Path}（sha256 不符）");
            }
        }

        if (failures.Count > 0)
        {
            _logger.LogError("完整性校验失败：{Count} 个文件不符", failures.Count);
            throw new IntegrityException(
                $"完整性校验失败，共 {failures.Count} 个文件不符。", failures);
        }

        var report = new IntegrityReport(
            true, mode, targets.Count, Array.Empty<string>(),
            mode == IntegrityCheckMode.Quick ? "入口文件校验通过。" : "全量完整性校验通过。",
            DateTimeOffset.Now);

        if (mode == IntegrityCheckMode.Quick)
        {
            WriteCache(manifest, signature, report);
        }
        else
        {
            // 全量通过后同步刷新快校验缓存，避免下次冷启动重复哈希
            WriteCache(manifest, ComputeSignature(SelectTargets(manifest, IntegrityCheckMode.Quick)), report);
        }

        return report;
    }

    private static IReadOnlyList<IntegrityEntry> SelectTargets(IntegrityManifest manifest, IntegrityCheckMode mode)
    {
        if (mode == IntegrityCheckMode.Full)
        {
            return manifest.Files;
        }

        var processName = Path.GetFileName(Environment.ProcessPath ?? string.Empty);
        var entry = manifest.Files.FirstOrDefault(
            f => !string.IsNullOrEmpty(processName) &&
                 string.Equals(Path.GetFileName(f.Path), processName, StringComparison.OrdinalIgnoreCase));

        if (entry is not null)
        {
            return new[] { entry };
        }

        var executables = manifest.Files
            .Where(f => f.Path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return executables.Length > 0 ? executables : new[] { manifest.Files[0] };
    }

    private string ResolveEntryPath(string relative)
    {
        var full = Path.GetFullPath(Path.Combine(
            _appDir, relative.Replace('/', Path.DirectorySeparatorChar)));
        var root = _appDir.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new IntegrityException($"清单条目逃逸程序目录：{relative}");
        }

        return full;
    }

    private (long MaxMtimeUtcTicks, long TotalSize) ComputeSignature(IReadOnlyList<IntegrityEntry> targets)
    {
        long maxTicks = 0;
        long totalSize = 0;
        foreach (var entry in targets)
        {
            var info = new FileInfo(ResolveEntryPath(entry.Path));
            if (info.Exists)
            {
                maxTicks = Math.Max(maxTicks, info.LastWriteTimeUtc.Ticks);
            }

            totalSize += entry.Size;
        }

        return (maxTicks, totalSize);
    }

    private bool TryReadCache(
        IntegrityManifest manifest,
        (long MaxMtimeUtcTicks, long TotalSize) signature,
        IntegrityCheckMode mode,
        out IntegrityReport report)
    {
        report = null!;
        if (!File.Exists(CachePath))
        {
            return false;
        }

        try
        {
            var json = File.ReadAllText(CachePath);
            var cache = JsonSerializer.Deserialize(json, IntegrityJsonContext.Default.IntegrityCacheEntryDto);
            if (cache is null ||
                !string.Equals(cache.Version, manifest.Version, StringComparison.Ordinal) ||
                cache.MaxMtimeUtcTicks != signature.MaxMtimeUtcTicks ||
                cache.TotalSize != signature.TotalSize ||
                !string.Equals(cache.Mode, mode.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            report = new IntegrityReport(
                true, mode, cache.CheckedFiles, Array.Empty<string>(),
                "完整性校验通过（缓存命中）。", cache.CheckedAt);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _logger.LogDebug(ex, "完整性缓存读取失败，忽略");
            return false;
        }
    }

    private void WriteCache(
        IntegrityManifest manifest,
        (long MaxMtimeUtcTicks, long TotalSize) signature,
        IntegrityReport report)
    {
        try
        {
            Directory.CreateDirectory(_paths.CacheDir);
            var cache = new IntegrityCacheEntryDto
            {
                Version = manifest.Version,
                MaxMtimeUtcTicks = signature.MaxMtimeUtcTicks,
                TotalSize = signature.TotalSize,
                Mode = report.Mode.ToString(),
                CheckedAt = report.CheckedAt,
                CheckedFiles = report.CheckedFiles,
            };
            File.WriteAllText(CachePath,
                JsonSerializer.Serialize(cache, IntegrityJsonContext.Default.IntegrityCacheEntryDto));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(ex, "完整性缓存写入失败，忽略");
        }
    }
}
