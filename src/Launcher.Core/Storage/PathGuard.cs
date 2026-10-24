namespace Launcher.Core.Storage;

public static class PathGuard
{
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    public static string ValidateId(string id, string paramName = "id")
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 64)
        {
            throw new ArgumentException("实例 ID 为空或超过 64 字符。", paramName);
        }

        if (id.Contains("..", StringComparison.Ordinal) ||
            id.EndsWith('.') || id.EndsWith(' '))
        {
            throw new ArgumentException($"非法实例 ID：{id}", paramName);
        }

        foreach (var c in id)
        {
            if (!char.IsLetterOrDigit(c) && c is not '.' and not '_' and not '-')
            {
                throw new ArgumentException($"实例 ID 仅允许字母数字与 . _ -：{id}", paramName);
            }
        }

        if (ReservedNames.Contains(id))
        {
            throw new ArgumentException($"实例 ID 不能使用 Windows 保留名：{id}", paramName);
        }

        return id;
    }

    public static string EnsureWithin(string root, string candidate)
    {
        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(candidate);
        var rootWithSeparator = fullRoot.EndsWith(Path.DirectorySeparatorChar)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"路径越界：{candidate}");
        }

        return fullPath;
    }
}
