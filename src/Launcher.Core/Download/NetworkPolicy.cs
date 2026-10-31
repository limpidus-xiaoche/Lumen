using System.Net;
using Launcher.Protocol;

namespace Launcher.Core.Download;

/// <summary>
/// 把代理设置应用到 <see cref="HttpClient.DefaultProxy"/>。
/// 只影响之后新建的 HttpClient；运行中已存在的客户端仍可能沿用旧代理。
/// </summary>
public static class NetworkPolicy
{
    private static readonly object Sync = new();
    private static IWebProxy? _originalProxy;
    private static bool _captured;

    /// <summary>应用设置；失败时返回 false 并通过 <paramref name="error"/> 给出原因，不抛异常。</summary>
    public static bool TryApply(AppSettings settings, out string? error)
    {
        try
        {
            Apply(settings);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// 应用代理设置。None 使用无代理实现，Manual 使用手动地址，System 恢复首次缓存的原值。
    /// </summary>
    public static void Apply(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        lock (Sync)
        {
            if (!_captured)
            {
                _originalProxy = HttpClient.DefaultProxy;
                _captured = true;
            }

            switch (settings.ProxyMode)
            {
                case ProxyMode.None:
                    HttpClient.DefaultProxy = NoProxy.Instance;
                    break;
                case ProxyMode.Manual:
                    var address = settings.ProxyAddress?.Trim();
                    if (string.IsNullOrEmpty(address))
                    {
                        throw new ArgumentException("手动代理模式需要填写代理地址");
                    }

                    if (!Uri.TryCreate(address, UriKind.Absolute, out var uri))
                    {
                        throw new ArgumentException($"代理地址无效：{address}");
                    }

                    HttpClient.DefaultProxy = new WebProxy(uri);
                    break;
                default:
                    // System：恢复首次调用时缓存的原值（通常为系统/环境变量代理）。
                    HttpClient.DefaultProxy = _originalProxy!;
                    break;
            }
        }
    }

    /// <summary>GetProxy 返回 null、IsBypassed 恒为 true，等价于“无代理”。</summary>
    private sealed class NoProxy : IWebProxy
    {
        public static readonly NoProxy Instance = new();

        public ICredentials? Credentials { get; set; }

        public Uri? GetProxy(Uri destination) => null;

        public bool IsBypassed(Uri host) => true;
    }
}
