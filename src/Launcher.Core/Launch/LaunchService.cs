using System.Diagnostics;
using System.Text;
using Launcher.Core.Accounts;
using Launcher.Core.Auth;
using Launcher.Core.Game;
using Launcher.Core.Install;
using Launcher.Core.Java;
using Launcher.Core.Meta;
using Launcher.Core.Storage;
using Launcher.Protocol;
using Microsoft.Extensions.Logging;

namespace Launcher.Core.Launch;

public sealed class LaunchService
{
    private readonly LauncherPaths _paths;
    private readonly AppSettingsStore _settings;
    private readonly SqliteStore _sqlite;
    private readonly VersionResolver _resolver;
    private readonly JavaLocator _javaLocator;
    private readonly RunningSessionStore _running;
    private readonly ILogger<LaunchService> _logger;
    private readonly AccountStore? _accounts;
    private readonly MicrosoftAuth? _microsoft;
    private readonly AuthlibInjector? _authlibInjector;

    public LaunchService(
        LauncherPaths paths,
        AppSettingsStore settings,
        SqliteStore sqlite,
        VersionResolver resolver,
        JavaLocator javaLocator,
        RunningSessionStore running,
        ILogger<LaunchService> logger,
        AccountStore? accounts = null,
        MicrosoftAuth? microsoft = null,
        AuthlibInjector? authlibInjector = null)
    {
        _paths = paths;
        _settings = settings;
        _sqlite = sqlite;
        _resolver = resolver;
        _javaLocator = javaLocator;
        _running = running;
        _logger = logger;
        _accounts = accounts;
        _microsoft = microsoft;
        _authlibInjector = authlibInjector;
    }

    public async Task<GameSession> LaunchAsync(
        string instanceId,
        string? username,
        IProgress<LaunchEvent> events,
        CancellationToken ct)
    {
        ArgFileWriter.SweepOld(TimeSpan.FromHours(1));

        var instances = await _sqlite.GetInstancesAsync(ct);
        var instance = instances.FirstOrDefault(i => i.Id == instanceId)
            ?? throw new LaunchException("E7001", $"实例不存在：{instanceId}");

        if (instance.State != InstanceState.Ready)
        {
            throw new LaunchException("E3003", $"实例当前状态为 {instance.State}，请先修复");
        }

        var effectiveVersionId = LoaderIds.EffectiveVersionId(
            instance.VersionId, instance.Loader, instance.LoaderVersion);
        var detail = await _resolver.ResolveAsync(effectiveVersionId, ct);
        var settings = _settings.Load();
        var java = SelectJava(detail, settings);

        var gameDir = instance.Isolation == IsolationLevel.None
            ? Path.Combine(_paths.SharedDir, "game")
            : Path.Combine(_paths.InstancesDir, instanceId);

        GameOptions.EnsureDefaultLanguage(gameDir);

        var versionDir = Path.Combine(_paths.SharedDir, "versions", instance.VersionId);
        var versionJar = Path.Combine(versionDir, $"{instance.VersionId}.jar");
        if (!File.Exists(versionJar))
        {
            throw new LaunchException("E3003", $"客户端 jar 不存在：{versionJar}");
        }

        var librariesDir = Path.Combine(_paths.SharedDir, "libraries");
        var nativesDir = Path.Combine(_paths.SharedDir, "natives", instance.VersionId, "x86_64");
        var assetsRoot = detail.AssetIndex?.Virtual == true
            ? Path.Combine(_paths.SharedDir, "assets", "virtual", "legacy")
            : Path.Combine(_paths.SharedDir, "assets");
        var assetIndexId = detail.AssetIndex?.Id ?? detail.Assets ?? instance.VersionId;

        var classpath = ArgumentBuilder.BuildClasspath(detail, librariesDir, versionJar);

        var credentials = await ResolveCredentialsAsync(username, ct);
        var undashed = credentials.Uuid.Replace("-", string.Empty);

        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["auth_player_name"] = credentials.Username,
            ["version_name"] = instance.VersionId,
            ["game_directory"] = gameDir,
            ["assets_root"] = assetsRoot,
            ["assets_index_name"] = assetIndexId,
            ["auth_uuid"] = undashed,
            ["auth_access_token"] = credentials.AccessToken,
            ["user_type"] = credentials.UserType,
            ["version_type"] = "Lumen",
            ["resolution_width"] = "854",
            ["resolution_height"] = "480",
            ["launcher_name"] = "Lumen",
            ["launcher_version"] = "0.1.0",
            ["clientid"] = string.Empty,
            ["auth_xuid"] = string.Empty,
            ["user_properties"] = "{}",
            ["quickPlayPath"] = Path.Combine(gameDir, "quickPlay"),
            ["quickPlaySingleplayer"] = string.Empty,
            ["quickPlayMultiplayer"] = string.Empty,
        };

        var extraJvmArgs = new List<string>();
        if (credentials.AuthlibServer is not null)
        {
            if (_authlibInjector is null)
            {
                throw new LaunchException("E7001", "authlib-injector 未注入，无法启动外置账户。");
            }

            extraJvmArgs.Add(await _authlibInjector.BuildJavaAgentAsync(credentials.AuthlibServer, ct));
        }

        var useG1 = JavaVersionPolicy.RequiredMajor(detail) >= 21;
        var command = ArgumentBuilder.Build(
            detail, classpath, gameDir, assetsRoot, assetIndexId, nativesDir, librariesDir,
            variables, settings.MaxMemoryMb, extraJvmArgs, useG1);

        var estimatedLength = command.Args.Sum(a => (long)a.Length + 3);
        string? argFile = null;
        IReadOnlyList<string> startArgs = command.Args;
        if (estimatedLength > 24_000)
        {
            if (java.MajorVersion < 9)
            {
                throw new LaunchException("E5003", "Java 8 命令行过长，尚未实现 wrapper");
            }

            argFile = ArgFileWriter.Write(command.Args, java.MajorVersion);
            startArgs = [$"@{argFile}"];
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = java.Path,
            WorkingDirectory = gameDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var arg in startArgs)
        {
            startInfo.ArgumentList.Add(arg);
        }

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        try
        {
            if (!process.Start())
            {
                throw new LaunchException("E5001", "无法启动 Java 进程");
            }
        }
        catch (Exception ex)
        {
            if (argFile is not null)
            {
                ArgFileWriter.DeleteWithRetry(argFile);
            }

            process.Dispose();
            throw new LaunchException("E5001", $"启动 Java 失败：{ex.Message}", ex);
        }

        var startedAt = DateTimeOffset.UtcNow;
        _running.Add(new RunningSession(instanceId, process.Id, startedAt, instance.VersionId));
        events.Report(new LaunchEvent(LaunchEventKind.Started, $"游戏进程已启动（PID {process.Id}）"));

        var firstLine = 0;
        async Task PumpAsync(StreamReader reader, LaunchEventKind kind)
        {
            try
            {
                while (true)
                {
                    var line = await reader.ReadLineAsync(CancellationToken.None);
                    if (line is null)
                    {
                        break;
                    }

                    if (Interlocked.Exchange(ref firstLine, 1) == 0 && argFile is not null)
                    {
                        ArgFileWriter.DeleteWithRetry(argFile);
                    }

                    events.Report(new LaunchEvent(kind, line));
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "读取游戏输出结束");
            }
        }

        var stdoutTask = PumpAsync(process.StandardOutput, LaunchEventKind.Stdout);
        var stderrTask = PumpAsync(process.StandardError, LaunchEventKind.Stderr);

        if (argFile is not null)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                ArgFileWriter.DeleteWithRetry(argFile);
            });
        }

        var exit = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = Task.Run(async () =>
        {
            await process.WaitForExitAsync();
            await Task.WhenAll(stdoutTask, stderrTask);
            var code = process.ExitCode;
            var hint = ExitCodeAnalyzer.Analyze(code, gameDir);
            events.Report(new LaunchEvent(LaunchEventKind.Exited, $"游戏已退出（exit code {code}）", code, CrashHint: hint));
            _running.Remove(instanceId);
            process.Dispose();
            exit.TrySetResult(code);
        });

        return new GameSession(instanceId, process.Id, exit.Task);
    }

    public Task StopGameAsync(string instanceId, StopMode mode, CancellationToken ct)
    {
        var session = _running.Find(instanceId)
            ?? throw new LaunchException("E7001", $"没有正在运行的实例：{instanceId}");

        try
        {
            using var process = Process.GetProcessById(session.ProcessId);
            if (mode == StopMode.Graceful)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(10_000))
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            else
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (ArgumentException)
        {
            // 进程已退出。
        }

        _running.Remove(instanceId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RunningSession>> GetRunningSessionsAsync(CancellationToken ct) =>
        Task.FromResult(_running.GetAlive());

    /// <summary>
    /// 启动凭据来源：显式离线用户名 &gt; 当前激活账户 &gt; 默认离线 Player。
    /// </summary>
    private async Task<LaunchCredentials> ResolveCredentialsAsync(string? username, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(username))
        {
            var offline = OfflineAuth.Create(username.Trim());
            return new LaunchCredentials(offline.Username, offline.Uuid, offline.AccessToken, "legacy", null);
        }

        var account = _accounts?.GetActive();
        if (account is null)
        {
            var fallback = OfflineAuth.Create("Player");
            return new LaunchCredentials(fallback.Username, fallback.Uuid, fallback.AccessToken, "legacy", null);
        }

        if (account.Type == AuthMode.Microsoft &&
            _microsoft is not null &&
            account.RefreshToken is { Length: > 0 } refreshToken &&
            account.ExpiresAt is { } expiresAt &&
            expiresAt <= DateTimeOffset.UtcNow.AddMinutes(5))
        {
            var refreshed = await _microsoft.RefreshAsync(refreshToken, ct);
            account = account with
            {
                Username = refreshed.Username,
                Uuid = refreshed.Uuid,
                AccessToken = refreshed.AccessToken,
                RefreshToken = refreshed.RefreshToken,
                ExpiresAt = refreshed.ExpiresAt,
            };
            _accounts!.Upsert(account);
            _logger.LogInformation("Microsoft 令牌已刷新：{Username}", account.Username);
        }

        if (account.Type == AuthMode.Authlib && string.IsNullOrWhiteSpace(account.ServerUrl))
        {
            throw new LaunchException("E7001", "外置账户缺少服务器地址，请重新登录。");
        }

        var userType = account.Type switch
        {
            AuthMode.Microsoft => "msa",
            AuthMode.Authlib => "mojang",
            _ => "legacy",
        };

        return new LaunchCredentials(account.Username, account.Uuid, account.AccessToken, userType, account.ServerUrl);
    }

    private sealed record LaunchCredentials(
        string Username,
        string Uuid,
        string AccessToken,
        string UserType,
        string? AuthlibServer);

    private JavaRuntime SelectJava(VersionDetail detail, AppSettings settings)
    {
        var runtimes = _javaLocator.Detect().ToList();
        if (!string.IsNullOrWhiteSpace(settings.JavaPath) &&
            File.Exists(settings.JavaPath) &&
            JavaVersionParser.TryRead(settings.JavaPath, out var preferred) &&
            !runtimes.Any(r => string.Equals(r.Path, preferred.Path, StringComparison.OrdinalIgnoreCase)))
        {
            runtimes.Add(preferred);
        }

        return JavaVersionPolicy.Select(detail, runtimes, settings.JavaPath);
    }
}
