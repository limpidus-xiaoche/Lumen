using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Launcher.Core.Download;

namespace Launcher.Core.Auth;

public sealed record AuthlibLoginResult(
    string Username,
    string Uuid,
    string AccessToken,
    string ClientToken,
    string ServerUrl);

public sealed class AuthlibAuth
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly Action<string> _validateUrl;

    public AuthlibAuth(HttpClient? http = null, Action<string>? validateUrl = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _validateUrl = validateUrl ?? UrlGuard.ValidatePublicHttps;
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Lumen/0.1");
        }
    }

    public async Task<AuthlibLoginResult> LoginAsync(
        string serverUrl, string username, string password, CancellationToken ct)
    {
        var baseUrl = Normalize(serverUrl);
        var url = $"{baseUrl}/authserver/authenticate";
        _validateUrl(url);

        var clientToken = Guid.NewGuid().ToString("N");
        var body = new
        {
            username,
            password,
            clientToken,
            agent = new { name = "Minecraft", version = 1 },
            requestUser = false,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        using var response = await _http.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);

        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new AuthException("E6005", ParseError(text) ?? "外置登录失败");
        }

        var result = JsonSerializer.Deserialize<AuthlibResponse>(text, JsonOptions)
            ?? throw new AuthException("E6005", "外置登录响应解析失败");

        var profile = result.SelectedProfile;
        if (profile is null || string.IsNullOrWhiteSpace(profile.Id) || string.IsNullOrWhiteSpace(profile.Name))
        {
            throw new AuthException("E6005", "外置登录未返回可用角色");
        }

        return new AuthlibLoginResult(
            profile.Name,
            profile.Id,
            result.AccessToken ?? throw new AuthException("E6005", "外置登录未返回 accessToken"),
            result.ClientToken ?? clientToken,
            baseUrl);
    }

    public static string Normalize(string serverUrl)
    {
        var trimmed = serverUrl.Trim().TrimEnd('/');
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new AuthException("E6005", "外置登录地址必须是 https URL");
        }

        return trimmed;
    }

    private static string? ParseError(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.TryGetProperty("errorMessage", out var message))
            {
                return message.GetString();
            }

            if (document.RootElement.TryGetProperty("error", out var error))
            {
                return error.GetString();
            }
        }
        catch (JsonException)
        {
            // 忽略。
        }

        return null;
    }

    private sealed class AuthlibResponse
    {
        [JsonPropertyName("accessToken")] public string? AccessToken { get; set; }
        [JsonPropertyName("clientToken")] public string? ClientToken { get; set; }
        [JsonPropertyName("selectedProfile")] public AuthlibProfile? SelectedProfile { get; set; }
    }

    private sealed class AuthlibProfile
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    }
}
