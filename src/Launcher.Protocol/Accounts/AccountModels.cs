namespace Launcher.Protocol;

public enum AuthMode
{
    Offline,
    Microsoft,
    Authlib,
}

public sealed record LoginRequest(
    AuthMode Mode,
    string? Username,
    string? ServerUrl,
    string? Password);

public enum LoginEventKind
{
    DeviceCode,
    WaitingForUser,
    Succeeded,
    Failed,
}

public sealed record LoginEvent(
    LoginEventKind Kind,
    string Message,
    string? UserCode = null,
    string? VerificationUrl = null);

public sealed record AccountSummary(
    string Id,
    AuthMode Type,
    string Username,
    string Uuid,
    string? AvatarUrl,
    bool IsActive,
    DateTimeOffset? ExpiresAt);
