using System.Net;
using System.Text;
using Launcher.Core.Auth;
using Launcher.Protocol;
using Xunit;

namespace Launcher.Core.Tests;

public sealed class MicrosoftAuthTests
{
    [Fact]
    public async Task DeviceCode_Flow_Returns_Minecraft_Profile()
    {
        var auth = new MicrosoftAuth(new HttpClient(new MicrosoftHandler()), _ => { });
        var events = new List<LoginEvent>();

        var result = await auth.LoginAsync(new SyncProgress<LoginEvent>(events.Add), CancellationToken.None);

        Assert.Equal("Steve", result.Username);
        Assert.Equal("0123456789abcdef0123456789abcdef", result.Uuid);
        Assert.Equal("mc-token", result.AccessToken);
        Assert.Equal("refresh-token", result.RefreshToken);
        Assert.True(result.ExpiresAt > DateTimeOffset.UtcNow);
        Assert.Contains(events, e => e.Kind == LoginEventKind.DeviceCode && e.UserCode == "ABCD1234");
        Assert.Contains(events, e => e.Kind == LoginEventKind.WaitingForUser);
    }

    [Fact]
    public async Task Refresh_Rotates_Tokens()
    {
        var handler = new MicrosoftHandler(refreshMode: true);
        var auth = new MicrosoftAuth(new HttpClient(handler), _ => { });

        var result = await auth.RefreshAsync("old-refresh", CancellationToken.None);

        Assert.Equal("Steve", result.Username);
        Assert.Equal("mc-token", result.AccessToken);
        Assert.Equal("refresh-token-2", result.RefreshToken);
    }

    [Fact]
    public async Task Xsts_Error_Is_Mapped()
    {
        var auth = new MicrosoftAuth(new HttpClient(new MicrosoftHandler(xstsError: 2148916233)), _ => { });

        var ex = await Assert.ThrowsAsync<AuthException>(() =>
            auth.LoginAsync(new SyncProgress<LoginEvent>(_ => { }), CancellationToken.None));

        Assert.Equal("E6003", ex.ErrorCode);
        Assert.Contains("Xbox", ex.Message, StringComparison.Ordinal);
    }

    private sealed class MicrosoftHandler : HttpMessageHandler
    {
        private readonly bool _refreshMode;
        private readonly long? _xstsError;
        private int _deviceCodePolls;

        public MicrosoftHandler(bool refreshMode = false, long? xstsError = null)
        {
            _refreshMode = refreshMode;
            _xstsError = xstsError;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();

            if (url.Contains("devicecode", StringComparison.Ordinal))
            {
                return Json("""
                    {"device_code":"dc","user_code":"ABCD1234","verification_uri":"https://microsoft.com/link",
                     "expires_in":600,"interval":1,"message":"请在浏览器中登录"}
                    """);
            }

            if (url.Contains("/token", StringComparison.Ordinal))
            {
                var body = request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken);

                if (body.Contains("grant_type=refresh_token", StringComparison.Ordinal))
                {
                    return Json("""
                        {"access_token":"live-token-2","refresh_token":"refresh-token-2","expires_in":3600}
                        """);
                }

                if (_refreshMode)
                {
                    return Json("""
                        {"access_token":"live-token-2","refresh_token":"refresh-token-2","expires_in":3600}
                        """);
                }

                _deviceCodePolls++;
                if (_deviceCodePolls == 1)
                {
                    return Json("""{"error":"authorization_pending"}""", HttpStatusCode.BadRequest);
                }

                return Json("""
                    {"access_token":"live-token","refresh_token":"refresh-token","expires_in":3600}
                    """);
            }

            if (url.Contains("user.auth.xboxlive.com", StringComparison.Ordinal))
            {
                return Json("""{"Token":"xbl-token","DisplayClaims":{"xui":[{"uhs":"user-hash"}]}}""");
            }

            if (url.Contains("xsts.auth.xboxlive.com", StringComparison.Ordinal))
            {
                if (_xstsError is { } code)
                {
                    return Json($$"""{"XErr":{{code}}}""", HttpStatusCode.Unauthorized);
                }

                return Json("""{"Token":"xsts-token","DisplayClaims":{"xui":[{"xid":"123"}]}}""");
            }

            if (url.Contains("login_with_xbox", StringComparison.Ordinal))
            {
                return Json("""{"access_token":"mc-token","expires_in":86400}""");
            }

            if (url.Contains("minecraft/profile", StringComparison.Ordinal))
            {
                return Json("""{"id":"0123456789abcdef0123456789abcdef","name":"Steve"}""");
            }

            throw new InvalidOperationException($"未预期的请求：{url}");
        }

        private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
            new(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
    }
}

public sealed class AuthlibAuthTests
{
    [Fact]
    public async Task Login_Returns_Selected_Profile()
    {
        var auth = new AuthlibAuth(
            new HttpClient(new StubHandler(_ => Json("""
                {"accessToken":"a-token","clientToken":"c-token",
                 "selectedProfile":{"id":"0123456789abcdef0123456789abcdef","name":"Alex"}}
                """))),
            _ => { });

        var result = await auth.LoginAsync("https://littleskin.example/", "alex", "pw", CancellationToken.None);

        Assert.Equal("Alex", result.Username);
        Assert.Equal("0123456789abcdef0123456789abcdef", result.Uuid);
        Assert.Equal("a-token", result.AccessToken);
        Assert.Equal("c-token", result.ClientToken);
        Assert.Equal("https://littleskin.example", result.ServerUrl);
    }

    [Fact]
    public async Task Error_Message_Is_Surfaced()
    {
        var auth = new AuthlibAuth(
            new HttpClient(new StubHandler(_ => Json(
                """{"error":"ForbiddenOperationException","errorMessage":"密码错误"}""",
                HttpStatusCode.Forbidden))),
            _ => { });

        var ex = await Assert.ThrowsAsync<AuthException>(() =>
            auth.LoginAsync("https://littleskin.example", "alex", "bad", CancellationToken.None));

        Assert.Equal("E6005", ex.ErrorCode);
        Assert.Equal("密码错误", ex.Message);
    }

    [Fact]
    public void Normalize_Rejects_Non_Https()
    {
        var ex = Assert.Throws<AuthException>(() => AuthlibAuth.Normalize("http://littleskin.example"));
        Assert.Equal("E6005", ex.ErrorCode);
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_handler(request));
    }
}

internal sealed class SyncProgress<T> : IProgress<T>
{
    private readonly Action<T> _report;

    public SyncProgress(Action<T> report)
    {
        _report = report;
    }

    public void Report(T value) => _report(value);
}
