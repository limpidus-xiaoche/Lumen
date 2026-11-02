using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Launcher.Core.Storage;

namespace Launcher.Core.Launch;

/// <summary>
/// Java 8 没有 @argfile，超长 classpath 只能靠通配符目录：
/// 把每个 jar 按原顺序硬链接（失败则复制）到 <c>cache/cp8/&lt;hash&gt;/</c>，
/// 文件名前缀 <c>0001-</c> 保证按字母序展开时仍是原始 classpath 顺序。
/// </summary>
public static class Java8ClasspathFarm
{
    public const string FarmFolderName = "cp8";

    private const int DirectoryHashLength = 12;
    private const int MaxEntryNameLength = 100;

    /// <summary>
    /// 生成（或复用）通配 classpath 目录。单个 jar 缺失只跳过并计入
    /// <c>MissingCount</c>，不阻断启动；目录名由完整 classpath 列表的哈希决定，天然幂等。
    /// </summary>
    public static (string WildcardClasspath, int JarCount, int MissingCount) Prepare(
        IReadOnlyList<string> classpath,
        string cacheRoot,
        string? preferredBaseDir = null)
    {
        ArgumentNullException.ThrowIfNull(classpath);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheRoot);

        var baseDir = string.IsNullOrWhiteSpace(preferredBaseDir) ? cacheRoot : preferredBaseDir;
        var farmDir = Path.Combine(baseDir, FarmFolderName, ComputeDirectoryName(classpath));
        PathGuard.EnsureWithin(baseDir, farmDir);
        Directory.CreateDirectory(farmDir);

        var expected = new List<(string Source, string Target)>();
        var expectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var missing = 0;

        for (var i = 0; i < classpath.Count; i++)
        {
            var source = classpath[i];
            if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
            {
                missing++;
                continue;
            }

            var target = Path.Combine(farmDir, BuildEntryFileName(i + 1, source));
            expected.Add((source, target));
            expectedNames.Add(Path.GetFileName(target));
        }

        // 清掉本轮不再需要的旧条目（例如原 jar 已删除但硬链接仍指向内容）。
        foreach (var file in Directory.EnumerateFiles(farmDir))
        {
            var name = Path.GetFileName(file);
            if (IsNumberedEntry(name) && !expectedNames.Contains(name))
            {
                TryDelete(file);
            }
        }

        var deployed = 0;
        foreach (var (source, target) in expected)
        {
            if (!IsUpToDate(source, target))
            {
                ReplaceEntry(source, target);
            }

            deployed++;
        }

        var wildcard = Path.Combine(farmDir, "*");
        return (wildcard, deployed, missing);
    }

    /// <summary>目标文件名：序号前缀 + 过滤非法字符后的原文件名，保证同目录内唯一且可排序。</summary>
    public static string BuildEntryFileName(int sequence, string sourcePath)
    {
        if (sequence < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "序号从 1 开始");
        }

        var name = Path.GetFileName(sourcePath);
        if (string.IsNullOrEmpty(name))
        {
            name = "entry.jar";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            builder.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        }

        var sanitized = builder.ToString();
        if (sanitized.Length > MaxEntryNameLength)
        {
            var extension = Path.GetExtension(sanitized);
            sanitized = extension.Length is > 0 and <= 20
                ? string.Concat(sanitized.AsSpan(0, MaxEntryNameLength - extension.Length), extension)
                : sanitized[..MaxEntryNameLength];
        }

        return $"{sequence:D4}-{sanitized}";
    }

    /// <summary>classpath 列表（按原始顺序）的 SHA-256 前 12 位，作为缓存目录名。</summary>
    public static string ComputeDirectoryName(IReadOnlyList<string> classpath)
    {
        ArgumentNullException.ThrowIfNull(classpath);

        var joined = string.Join("\n", classpath);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(joined));
        return Convert.ToHexString(hash)[..DirectoryHashLength].ToLowerInvariant();
    }

    private static void ReplaceEntry(string source, string target)
    {
        var temp = $"{target}.tmp-{Guid.NewGuid():N}";
        try
        {
            if (!TryCreateHardLink(temp, source))
            {
                File.Copy(source, temp, overwrite: true);
            }

            File.Move(temp, target, overwrite: true);
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    private static bool IsUpToDate(string source, string target)
    {
        var targetInfo = new FileInfo(target);
        if (!targetInfo.Exists)
        {
            return false;
        }

        var sourceInfo = new FileInfo(source);
        return targetInfo.Length == sourceInfo.Length &&
               targetInfo.LastWriteTimeUtc == sourceInfo.LastWriteTimeUtc;
    }

    private static bool IsNumberedEntry(string name) =>
        name.Length > 5 &&
        name[..4].All(char.IsAsciiDigit) &&
        name[4] == '-';

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // 残留条目由下次 Prepare 的清理阶段处理。
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static bool TryCreateHardLink(string linkPath, string existingPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            return CreateHardLinkW(linkPath, existingPath, IntPtr.Zero);
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLinkW(string newFileName, string existingFileName, IntPtr securityAttributes);
}
