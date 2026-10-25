using System.Security.Cryptography;

namespace Launcher.Core.Update;

/// <summary>更新包校验：先比 size 再比 sha256，任何不符抛 E8001（§12.1 规则 3）。</summary>
public static class UpdatePackageVerifier
{
    public static async Task<string> VerifyFileAsync(
        string path, long expectedSize, string expectedSha256, CancellationToken ct = default)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
        {
            throw new UpdateException($"更新包不存在：{path}");
        }

        if (info.Length != expectedSize)
        {
            throw new UpdateException($"更新包 size 不符：实际 {info.Length}，期望 {expectedSize}");
        }

        string actual;
        await using (var stream = info.OpenRead())
        {
            actual = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct));
        }

        if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new UpdateException("更新包 sha256 不符。");
        }

        return actual;
    }
}
