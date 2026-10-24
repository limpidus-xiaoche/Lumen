namespace Launcher.Protocol;

public enum InstallStage
{
    VersionJson,
    ClientJar,
    Libraries,
    Natives,
    Assets,
    Loader,
    Finalizing,
    Done,
}

public sealed record InstallProgress(
    InstallStage Stage,
    double Percent,
    double SpeedBytesPerSec,
    long BytesDownloaded,
    long TotalBytes,
    string? CurrentFile,
    string Message,
    string? ErrorCode = null);

public sealed record ImportProgress(
    string Stage,
    double Percent,
    long BytesDone,
    long TotalBytes,
    string? CurrentPath);

public enum LaunchEventKind
{
    Stdout,
    Stderr,
    Started,
    Exited,
    Crashed,
}

public sealed record LaunchEvent(
    LaunchEventKind Kind,
    string Line,
    int? ExitCode = null,
    string? ErrorCode = null,
    string? CrashHint = null);

public enum StopMode
{
    Graceful,
    Force,
}

public sealed record GameSession(string InstanceId, int ProcessId, Task<int> ExitTask);

public sealed record RunningSession(
    string InstanceId,
    int ProcessId,
    DateTimeOffset StartedAt,
    string? VersionId);

public enum TaskKind
{
    Install,
    Repair,
    Modpack,
    Launch,
    Update,
    Import,
}

public enum TaskState
{
    Queued,
    Running,
    Paused,
    Succeeded,
    Failed,
    Cancelled,
}

public sealed record TaskInfo(
    string TaskId,
    string? InstanceId,
    TaskKind Kind,
    TaskState State,
    double Percent,
    string Message,
    int Attempts,
    string? ErrorCode);
