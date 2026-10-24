using System.Diagnostics;
using System.Text.Json;
using Launcher.Core.Storage;
using Launcher.Protocol;

namespace Launcher.Core.Launch;

public sealed class RunningSessionStore
{
    private readonly string _path;
    private readonly object _sync = new();

    public RunningSessionStore(LauncherPaths paths)
    {
        _path = Path.Combine(paths.LocalRoot, "running.json");
    }

    public void Add(RunningSession session)
    {
        lock (_sync)
        {
            var list = LoadRaw();
            list.RemoveAll(s => s.InstanceId == session.InstanceId || s.ProcessId == session.ProcessId);
            list.Add(session);
            SaveRaw(list);
        }
    }

    public void Remove(string instanceId)
    {
        lock (_sync)
        {
            var list = LoadRaw();
            list.RemoveAll(s => s.InstanceId == instanceId);
            SaveRaw(list);
        }
    }

    public RunningSession? Find(string instanceId)
    {
        lock (_sync)
        {
            return LoadRaw().FirstOrDefault(s => s.InstanceId == instanceId);
        }
    }

    public IReadOnlyList<RunningSession> GetAlive()
    {
        lock (_sync)
        {
            var list = LoadRaw();
            var alive = new List<RunningSession>();
            foreach (var session in list)
            {
                if (IsAlive(session))
                {
                    alive.Add(session);
                }
            }

            if (alive.Count != list.Count)
            {
                SaveRaw(alive);
            }

            return alive;
        }
    }

    private static bool IsAlive(RunningSession session)
    {
        try
        {
            using var process = Process.GetProcessById(session.ProcessId);
            if (process.HasExited)
            {
                return false;
            }

            var start = new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero);
            return Math.Abs((start - session.StartedAt).TotalSeconds) < 5;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private List<RunningSession> LoadRaw()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return [];
            }

            return JsonSerializer.Deserialize<List<RunningSession>>(File.ReadAllText(_path)) ?? [];
        }
        catch (Exception)
        {
            return [];
        }
    }

    private void SaveRaw(List<RunningSession> list)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(list));
    }
}
