using System.Text;
using Launcher.Core.Accounts;
using Launcher.Core.Auth;
using Launcher.Core.Storage;
using Launcher.Protocol;
using Xunit;

namespace Launcher.Core.Tests;

public sealed class AccountStoreTests
{
    [Fact]
    public void First_Upsert_Becomes_Active_And_Survives_Reload()
    {
        var root = TempRoot();
        var account = Offline("Player");
        var store = Create(root, new XorProtector());

        store.Upsert(account);

        Assert.Equal(account.Id, store.ActiveId);

        var reloaded = Create(root, new XorProtector());
        var active = reloaded.GetActive();
        Assert.NotNull(active);
        Assert.Equal("Player", active.Username);
        Assert.Equal(account.Uuid, active.Uuid);
        Assert.Equal(account.AccessToken, active.AccessToken);
    }

    [Fact]
    public void Stored_File_Does_Not_Contain_Plaintext()
    {
        var root = TempRoot();
        var account = Offline("SecretPlayer");
        Create(root, new XorProtector()).Upsert(account);

        var raw = File.ReadAllBytes(Path.Combine(root, "accounts.dat"));
        var text = Encoding.UTF8.GetString(raw);
        Assert.DoesNotContain("SecretPlayer", text, StringComparison.Ordinal);
        Assert.DoesNotContain(account.AccessToken, text, StringComparison.Ordinal);
    }

    [Fact]
    public void SetActive_Switches_And_Remove_Falls_Back()
    {
        var root = TempRoot();
        var store = Create(root, new XorProtector());
        var first = Offline("First");
        var second = Offline("Second");

        store.Upsert(first);
        store.Upsert(second);
        Assert.Equal(first.Id, store.ActiveId);

        store.SetActive(second.Id);
        Assert.Equal(second.Id, store.ActiveId);
        Assert.Single(store.GetSummaries(), a => a.IsActive);

        store.Remove(second.Id);
        Assert.Equal(first.Id, store.ActiveId);
        Assert.Single(store.GetAll());
    }

    [Fact]
    public void SetActive_Unknown_Throws()
    {
        var store = Create(TempRoot(), new XorProtector());
        Assert.Throws<KeyNotFoundException>(() => store.SetActive("missing"));
    }

    [Fact]
    public void Corrupt_File_Returns_Empty_Accounts()
    {
        var root = TempRoot();
        File.WriteAllText(Path.Combine(root, "accounts.dat"), "not-encrypted-json");

        var store = Create(root, new XorProtector());
        Assert.Empty(store.GetAll());
        Assert.Null(store.ActiveId);
    }

    [Fact]
    public void Protector_Failure_Is_Treated_As_ReLogin()
    {
        var root = TempRoot();
        Create(root, new XorProtector()).Upsert(Offline("Player"));

        var store = Create(root, new ThrowingProtector());
        Assert.Empty(store.GetSummaries());
        Assert.Null(store.GetActive());
    }

    private static AccountStore Create(string root, ICredentialProtector protector) =>
        new(new LauncherPaths(root, root), protector);

    private static StoredAccount Offline(string name)
    {
        var identity = OfflineAuth.Create(name);
        return new StoredAccount(
            $"offline:{identity.Uuid}",
            AuthMode.Offline,
            identity.Username,
            identity.Uuid,
            AvatarUrl: null,
            identity.AccessToken,
            RefreshToken: null,
            ExpiresAt: null);
    }

    private static string TempRoot()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lumen-accounts", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private sealed class XorProtector : ICredentialProtector
    {
        private const byte Key = 0x5A;

        public byte[] Protect(byte[] plaintext) => plaintext.Select(b => (byte)(b ^ Key)).ToArray();

        public byte[] Unprotect(byte[] ciphertext) => ciphertext.Select(b => (byte)(b ^ Key)).ToArray();
    }

    private sealed class ThrowingProtector : ICredentialProtector
    {
        public byte[] Protect(byte[] plaintext) => plaintext;

        public byte[] Unprotect(byte[] ciphertext) =>
            throw new InvalidOperationException("DPAPI 解密失败（模拟换机）");
    }
}
