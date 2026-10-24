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

            INSERT OR IGNORE INTO schema_meta(key, value) VALUES('version', '3');

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

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(ct);
    }
}
