using Launcher.Protocol;
using Microsoft.Data.Sqlite;

namespace Launcher.Core.Storage;

public sealed class SqliteStore
{
    private readonly string _connectionString;

    public SqliteStore(LauncherPaths paths)
    {
        Directory.CreateDirectory(paths.LocalRoot);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = paths.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        await ExecuteAsync(connection, "PRAGMA journal_mode=WAL;", ct);
        await ExecuteAsync(connection, "PRAGMA synchronous=NORMAL;", ct);

        await ExecuteAsync(connection, """
            CREATE TABLE IF NOT EXISTS schema_meta(
              key TEXT PRIMARY KEY,
              value TEXT NOT NULL);

            INSERT OR REPLACE INTO schema_meta(key, value) VALUES('version', '4');

            CREATE TABLE IF NOT EXISTS instances(
              id TEXT PRIMARY KEY,
              name TEXT NOT NULL,
              icon TEXT,
              root_path TEXT NOT NULL,
              version_id TEXT NOT NULL,
              loader TEXT NOT NULL,
              loader_version TEXT,
              isolation INTEGER NOT NULL,
              state TEXT NOT NULL,
              java_path TEXT,
              account_id TEXT,
              memory_mb INTEGER,
              extra_jvm_args TEXT,
              created_at TEXT NOT NULL,
              last_played_at TEXT);

            CREATE TABLE IF NOT EXISTS blobs(
              sha1 TEXT PRIMARY KEY,
              size INTEGER NOT NULL,
              local_path TEXT NOT NULL,
              verified_at TEXT);

            CREATE TABLE IF NOT EXISTS url_cache(
              url TEXT PRIMARY KEY,
              sha1 TEXT,
              etag TEXT,
              last_modified TEXT,
              checked_at TEXT);

            CREATE TABLE IF NOT EXISTS mirrors(
              host TEXT PRIMARY KEY,
              success INTEGER NOT NULL DEFAULT 0,
              failure INTEGER NOT NULL DEFAULT 0,
              latency_ms INTEGER,
              cooldown_until TEXT);

            CREATE TABLE IF NOT EXISTS download_queue(
              id TEXT PRIMARY KEY,
              task_id TEXT,
              instance_id TEXT,
              url TEXT,
              dest TEXT,
              sha1 TEXT,
              size INTEGER,
              state TEXT,
              priority INTEGER,
              attempts INTEGER DEFAULT 0,
              error TEXT,
              created_at TEXT,
              updated_at TEXT);

            CREATE TABLE IF NOT EXISTS install_journal(
              instance_id TEXT,
              step TEXT,
              status TEXT,
              detail TEXT,
              updated_at TEXT,
              PRIMARY KEY(instance_id, step));

            CREATE TABLE IF NOT EXISTS tasks(
              task_id TEXT PRIMARY KEY,
              instance_id TEXT,
              kind TEXT NOT NULL,
              state TEXT NOT NULL,
              percent REAL NOT NULL,
              message TEXT NOT NULL,
              attempts INTEGER NOT NULL DEFAULT 0,
              error_code TEXT,
              created_at TEXT NOT NULL,
              updated_at TEXT NOT NULL);
            """, ct);
    }

    public async Task<int> GetSchemaVersionAsync(CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM schema_meta WHERE key='version';";
        var value = await command.ExecuteScalarAsync(ct);
        return int.TryParse(value?.ToString(), out var version) ? version : 0;
    }

    public async Task<IReadOnlyList<InstanceSummary>> GetInstancesAsync(CancellationToken ct = default)
    {
        var result = new List<InstanceSummary>();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, version_id, loader, loader_version, isolation, state, icon, last_played_at
            FROM instances ORDER BY last_played_at DESC;
            """;

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new InstanceSummary(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                Enum.TryParse<LoaderKind>(reader.GetString(3), ignoreCase: true, out var loader) ? loader : LoaderKind.Vanilla,
                reader.IsDBNull(4) ? null : reader.GetString(4),
                (IsolationLevel)reader.GetInt32(5),
                Enum.TryParse<InstanceState>(reader.GetString(6), ignoreCase: true, out var state) ? state : InstanceState.Ready,
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : DateTimeOffset.Parse(reader.GetString(8))));
        }

        return result;
    }

    public async Task UpsertInstanceAsync(
        string id,
        string name,
        string rootPath,
        string versionId,
        LoaderKind loader,
        string? loaderVersion,
        IsolationLevel isolation,
        InstanceState state,
        string? javaPath = null,
        int? memoryMb = null,
        string extraJvmArgs = "",
        CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO instances(
              id, name, root_path, version_id, loader, loader_version, isolation, state,
              java_path, memory_mb, extra_jvm_args, created_at)
            VALUES($id, $name, $root, $version, $loader, $loaderVersion, $isolation, $state,
                   $java, $memory, $extra, $created)
            ON CONFLICT(id) DO UPDATE SET
              name = excluded.name,
              root_path = excluded.root_path,
              version_id = excluded.version_id,
              loader = excluded.loader,
              loader_version = excluded.loader_version,
              isolation = excluded.isolation,
              state = excluded.state,
              java_path = excluded.java_path,
              memory_mb = excluded.memory_mb,
              extra_jvm_args = excluded.extra_jvm_args;
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$root", rootPath);
        command.Parameters.AddWithValue("$version", versionId);
        command.Parameters.AddWithValue("$loader", loader.ToString());
        command.Parameters.AddWithValue("$loaderVersion", (object?)loaderVersion ?? DBNull.Value);
        command.Parameters.AddWithValue("$isolation", (int)isolation);
        command.Parameters.AddWithValue("$state", state.ToString());
        command.Parameters.AddWithValue("$java", (object?)javaPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$memory", (object?)memoryMb ?? DBNull.Value);
        command.Parameters.AddWithValue("$extra", extraJvmArgs);
        command.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateInstanceStateAsync(
        string id, InstanceState state, CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE instances SET state=$state WHERE id=$id;";
        command.Parameters.AddWithValue("$state", state.ToString());
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task RecordJournalAsync(
        string instanceId, string step, string status, string detail, CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO install_journal(instance_id, step, status, detail, updated_at)
            VALUES($instance, $step, $status, $detail, $updated)
            ON CONFLICT(instance_id, step) DO UPDATE SET
              status = excluded.status,
              detail = excluded.detail,
              updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$instance", instanceId);
        command.Parameters.AddWithValue("$step", step);
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$detail", detail);
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<(string Step, string Status, string Detail)>> GetJournalAsync(
        string instanceId, CancellationToken ct = default)
    {
        var result = new List<(string, string, string)>();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT step, status, detail FROM install_journal
            WHERE instance_id=$instance ORDER BY updated_at;
            """;
        command.Parameters.AddWithValue("$instance", instanceId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add((reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? string.Empty : reader.GetString(2)));
        }

        return result;
    }

    public async Task SaveTaskAsync(TaskInfo task, CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO tasks(task_id, instance_id, kind, state, percent, message, attempts, error_code, created_at, updated_at)
            VALUES($id, $instance, $kind, $state, $percent, $message, $attempts, $error, $created, $updated)
            ON CONFLICT(task_id) DO UPDATE SET
              state = excluded.state,
              percent = excluded.percent,
              message = excluded.message,
              attempts = excluded.attempts,
              error_code = excluded.error_code,
              updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$id", task.TaskId);
        command.Parameters.AddWithValue("$instance", (object?)task.InstanceId ?? DBNull.Value);
        command.Parameters.AddWithValue("$kind", task.Kind.ToString());
        command.Parameters.AddWithValue("$state", task.State.ToString());
        command.Parameters.AddWithValue("$percent", task.Percent);
        command.Parameters.AddWithValue("$message", task.Message);
        command.Parameters.AddWithValue("$attempts", task.Attempts);
        command.Parameters.AddWithValue("$error", (object?)task.ErrorCode ?? DBNull.Value);
        command.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(ct);
    }

    /// <summary>按更新时间倒序读取最近的任务，用于任务中心重启恢复。</summary>
    public async Task<IReadOnlyList<TaskInfo>> GetRecentTasksAsync(int limit, CancellationToken ct = default)
    {
        var result = new List<TaskInfo>();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT task_id, instance_id, kind, state, percent, message, attempts, error_code
            FROM tasks ORDER BY updated_at DESC LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", Math.Max(1, limit));
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new TaskInfo(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                Enum.TryParse<TaskKind>(reader.GetString(2), ignoreCase: true, out var kind) ? kind : TaskKind.Install,
                Enum.TryParse<TaskState>(reader.GetString(3), ignoreCase: true, out var state) ? state : TaskState.Failed,
                reader.GetDouble(4),
                reader.GetString(5),
                reader.GetInt32(6),
                reader.IsDBNull(7) ? null : reader.GetString(7)));
        }

        return result;
    }

    /// <summary>只保留最近 <paramref name="keep"/> 条任务记录，避免表无限增长。</summary>
    public async Task PruneTasksAsync(int keep, CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM tasks WHERE task_id NOT IN (
              SELECT task_id FROM tasks ORDER BY updated_at DESC LIMIT $keep);
            """;
        command.Parameters.AddWithValue("$keep", Math.Max(1, keep));
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(ct);
    }
}
