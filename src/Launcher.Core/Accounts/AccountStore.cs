using System.Text.Json;
using System.Text.Json.Serialization;
using Launcher.Core.Auth;
using Launcher.Core.Storage;
using Launcher.Protocol;

namespace Launcher.Core.Accounts;

public sealed record StoredAccount(
    string Id,
    AuthMode Type,
    string Username,
    string Uuid,
    string? AvatarUrl,
    string AccessToken,
    string? RefreshToken,
    DateTimeOffset? ExpiresAt,
    string? ServerUrl = null,
    string? ClientToken = null);

public sealed class AccountStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    private readonly string _path;
    private readonly ICredentialProtector _protector;
    private readonly object _sync = new();

    public AccountStore(LauncherPaths paths, ICredentialProtector? protector = null)
    {
        _path = Path.Combine(paths.LocalRoot, "accounts.dat");
        _protector = protector ?? new DpapiCredentialProtector();
    }

    public IReadOnlyList<AccountSummary> GetSummaries()
    {
        lock (_sync)
        {
            var file = Load();
            return file.Accounts.Select(a => new AccountSummary(
                a.Id, a.Type, a.Username, a.Uuid, a.AvatarUrl,
                IsActive: string.Equals(a.Id, file.ActiveId, StringComparison.Ordinal),
                a.ExpiresAt)).ToArray();
        }
    }

    public IReadOnlyList<StoredAccount> GetAll()
    {
        lock (_sync)
        {
            return Load().Accounts.ToArray();
        }
    }

    public string? ActiveId
    {
        get
        {
            lock (_sync)
            {
                return Load().ActiveId;
            }
        }
    }

    public StoredAccount? GetActive()
    {
        lock (_sync)
        {
            var file = Load();
            return file.Accounts.FirstOrDefault(a =>
                string.Equals(a.Id, file.ActiveId, StringComparison.Ordinal));
        }
    }

    public void Upsert(StoredAccount account)
    {
        lock (_sync)
        {
            var file = Load();
            file.Accounts.RemoveAll(a => a.Id == account.Id);
            file.Accounts.Add(account);
            file.ActiveId ??= account.Id;
            Save(file);
        }
    }

    public void SetActive(string accountId)
    {
        lock (_sync)
        {
            var file = Load();
            if (!file.Accounts.Any(a => a.Id == accountId))
            {
                throw new KeyNotFoundException($"账户不存在：{accountId}");
            }

            file.ActiveId = accountId;
            Save(file);
        }
    }

    public void Remove(string accountId)
    {
        lock (_sync)
        {
            var file = Load();
            file.Accounts.RemoveAll(a => a.Id == accountId);
            if (file.ActiveId == accountId)
            {
                file.ActiveId = file.Accounts.FirstOrDefault()?.Id;
            }

            Save(file);
        }
    }

    private AccountFile Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new AccountFile();
            }

            var cipher = File.ReadAllBytes(_path);
            var plain = _protector.Unprotect(cipher);
            var file = JsonSerializer.Deserialize<AccountFile>(plain, JsonOptions);
            return file ?? new AccountFile();
        }
        catch (Exception)
        {
            // 换机/DPAPI 失效：保留文件，返回空账户并要求重新登录。
            return new AccountFile();
        }
    }

    private void Save(AccountFile file)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var plain = JsonSerializer.SerializeToUtf8Bytes(file, JsonOptions);
        var cipher = _protector.Protect(plain);
        var temp = _path + ".tmp";
        File.WriteAllBytes(temp, cipher);
        File.Move(temp, _path, overwrite: true);
    }

    private sealed class AccountFile
    {
        [JsonPropertyName("accounts")] public List<StoredAccount> Accounts { get; set; } = [];
        [JsonPropertyName("active_id")] public string? ActiveId { get; set; }
    }
}
