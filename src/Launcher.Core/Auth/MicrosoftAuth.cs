using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Launcher.Core.Download;
using Launcher.Protocol;

namespace Launcher.Core.Auth;

public sealed record MicrosoftLoginResult(
    string Username,
    string Uuid,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    string? AvatarUrl);

public sealed class MicrosoftAuth
{
    public const string ClientId = "00000000402b5328";
    private const string Scope = "XboxLive.signin offline_access";
    private const string DeviceCodeUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode";
    private const string TokenUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
    private const string XboxAuthUrl = "https://user.auth.xboxlive.com/user/authenticate";
    private const string XstsUrl = "https://xsts.auth.xboxlive.com/xsts/authorize";
    private const string McLoginUrl = "https://api.minecraftservices.com/authentication/login_with_xbox";
    private const string McProfileUrl = "https://api.minecraftservices.com/minecraft/profile";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly Action<string> _validateUrl;

    public MicrosoftAuth(HttpClient? http = null, Action<string>? validateUrl = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _validateUrl = validateUrl ?? (url => UrlGuard.Validate(url));
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Lumen/0.1");
        }
    }

    public async Task<MicrosoftLoginResult> LoginAsync(
        IProgress<LoginEvent> progress, CancellationToken ct)
    {
        var device = await RequestDeviceCodeAsync(ct);
        progress.Report(new LoginEvent(
            LoginEventKind.DeviceCode,
            device.Message ?? "请在浏览器中完成 Microsoft 登录",
            device.UserCode,
            device.VerificationUri ?? device.VerificationUrl));

        var token = await PollAsync(device, progress, ct);
        return await CompleteAsync(token, ct);
    }

    public async Task<MicrosoftLoginResult> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var token = await PostFormAsync<TokenResponse>(TokenUrl, new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["scope"] = Scope,
        }, ct);

        if (string.IsNullOrWhiteSpace(token.AccessToken) || string.IsNullOrWhiteSpace(token.RefreshToken))
        {
            throw new AuthException("E6004", "Microsoft 令牌刷新失败，请重新登录");
        }

        return await CompleteAsync(token, ct);
    }

    private async Task<DeviceCodeResponse> RequestDeviceCodeAsync(CancellationToken ct) =>
        await PostFormAsync<DeviceCodeResponse>(DeviceCodeUrl, new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["scope"] = Scope,
        }, ct);

    private async Task<TokenResponse> PollAsync(
        DeviceCodeResponse device, IProgress<LoginEvent> progress, CancellationToken ct)
    {
        var interval = Math.Max(1, device.Interval);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(device.ExpiresIn > 0 ? device.ExpiresIn : 900);
        progress.Report(new LoginEvent(LoginEventKind.WaitingForUser, "等待 Microsoft 登录完成…"));

        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromSeconds(interval), ct);
            var (status, text) = await PostFormRawAsync(TokenUrl, new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                ["device_code"] = device.DeviceCode,
            }, ct);

            var token = JsonSerializer.Deserialize<TokenResponse>(text, JsonOptions) ?? new TokenResponse();
            if (status == HttpStatusCode.OK && !string.IsNullOrWhiteSpace(token.AccessToken))
            {
                return token;
            }

            if (string.Equals(token.Error, "authorization_pending", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(token.Error, "slow_down", StringComparison.OrdinalIgnoreCase))
            {
                interval += 5;
                continue;
            }

            throw new AuthException("E6001", token.ErrorDescription ?? token.Error ?? "Microsoft 登录失败");
        }

        throw new AuthException("E6002", "Microsoft 登录已超时，请重试");
    }

    private async Task<MicrosoftLoginResult> CompleteAsync(TokenResponse token, CancellationToken ct)
    {
        var xbl = await PostJsonAsync<XblResponse>(XboxAuthUrl, new
        {
            Properties = new
            {
                AuthMethod = "RPS",
                SiteName = "user.auth.xboxlive.com",
                RpsTicket = $"d={token.AccessToken}",
            },
            RelyingParty = "http://auth.xboxlive.com",
            TokenType = "JWT",
        }, ct);

        var uhs = xbl.DisplayClaims?.Xui.FirstOrDefault()?.Uhs
            ?? throw new AuthException("E6002", "Xbox 认证缺少 uhs");

        var (xstsStatus, xstsText) = await PostJsonRawAsync(XstsUrl, new
        {
            Properties = new
            {
                SandboxId = "RETAIL",
                UserTokens = new[] { xbl.Token },
            },
            RelyingParty = "rp://api.minecraftservices.com/",
            TokenType = "JWT",
        }, ct);

        if (xstsStatus != HttpStatusCode.OK)
        {
            throw new AuthException("E6003", ParseXstsError(xstsText));
        }

        var xsts = JsonSerializer.Deserialize<XblResponse>(xstsText, JsonOptions)
            ?? throw new AuthException("E6003", "XSTS 响应解析失败");

        var mc = await PostJsonAsync<McLoginResponse>(McLoginUrl, new
        {
            identityToken = $"XBL3.0 x={uhs};{xsts.Token}",
        }, ct);

        var profile = await GetProfileAsync(mc.AccessToken, ct);
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(mc.ExpiresIn > 0 ? mc.ExpiresIn : 86400);

        return new MicrosoftLoginResult(
            profile.Name,
            profile.Id,
            mc.AccessToken,
            token.RefreshToken ?? throw new AuthException("E6004", "Microsoft 未返回 refresh_token"),
            expiresAt,
            AvatarUrl: null);
    }

    private async Task<ProfileResponse> GetProfileAsync(string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, McProfileUrl);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        _validateUrl(McProfileUrl);
        using var response = await _http.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new AuthException("E6003", "无法获取 Minecraft 档案，账号可能未拥有游戏");
        }

        return JsonSerializer.Deserialize<ProfileResponse>(text, JsonOptions)
            ?? throw new AuthException("E6003", "Minecraft 档案解析失败");
    }

    private async Task<T> PostJsonAsync<T>(string url, object body, CancellationToken ct)
    {
        var (status, text) = await PostJsonRawAsync(url, body, ct);
        if (status != HttpStatusCode.OK)
        {
            throw new AuthException("E6002", $"请求失败（{(int)status}）：{text}");
        }

        return JsonSerializer.Deserialize<T>(text, JsonOptions)
            ?? throw new AuthException("E6002", "响应解析失败");
    }

    private async Task<(HttpStatusCode Status, string Text)> PostJsonRawAsync(
        string url, object body, CancellationToken ct)
    {
        _validateUrl(url);
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        using var response = await _http.SendAsync(request, ct);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(ct));
    }

    private async Task<T> PostFormAsync<T>(
        string url, Dictionary<string, string> form, CancellationToken ct)
    {
        var (status, text) = await PostFormRawAsync(url, form, ct);
        if (status != HttpStatusCode.OK)
        {
            throw new AuthException("E6001", $"请求失败（{(int)status}）：{text}");
        }

        return JsonSerializer.Deserialize<T>(text, JsonOptions)
            ?? throw new AuthException("E6001", "响应解析失败");
    }

    private async Task<(HttpStatusCode Status, string Text)> PostFormRawAsync(
        string url, Dictionary<string, string> form, CancellationToken ct)
    {
        _validateUrl(url);
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(form),
        };
        using var response = await _http.SendAsync(request, ct);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(ct));
    }

    private static string ParseXstsError(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.TryGetProperty("XErr", out var xerr))
            {
                var code = xerr.GetInt64();
                return code switch
                {
                    2148916233 => "该 Microsoft 账号没有 Xbox 档案",
                    2148916238 => "儿童账号需要先加入 Microsoft 家庭",
                    _ => $"Xbox 认证失败（XErr {code}）",
                };
            }
        }
        catch (JsonException)
        {
            // 忽略，返回通用错误。
        }

        return "Xbox 认证失败";
    }

    private sealed class DeviceCodeResponse
    {
        [JsonPropertyName("device_code")] public string DeviceCode { get; set; } = string.Empty;
        [JsonPropertyName("user_code")] public string UserCode { get; set; } = string.Empty;
        [JsonPropertyName("verification_uri")] public string? VerificationUri { get; set; }
        [JsonPropertyName("verification_url")] public string? VerificationUrl { get; set; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
        [JsonPropertyName("interval")] public int Interval { get; set; }
        [JsonPropertyName("message")] public string? Message { get; set; }
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
        [JsonPropertyName("error")] public string? Error { get; set; }
        [JsonPropertyName("error_description")] public string? ErrorDescription { get; set; }
    }

    private sealed class XblResponse
    {
        [JsonPropertyName("Token")] public string Token { get; set; } = string.Empty;
        [JsonPropertyName("DisplayClaims")] public DisplayClaims? DisplayClaims { get; set; }
    }

    private sealed class DisplayClaims
    {
        [JsonPropertyName("xui")] public List<Xui> Xui { get; set; } = [];
    }

    private sealed class Xui
    {
        [JsonPropertyName("uhs")] public string Uhs { get; set; } = string.Empty;
    }

    private sealed class McLoginResponse
    {
        [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    }

    private sealed class ProfileResponse
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    }
}
