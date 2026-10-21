using System.Text;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Application.Security;
using Sockseek.Infrastructure.Security;

namespace Sockseek.Infrastructure.Tests.Security;

[TestClass]
public sealed class SecretStoreTests
{
    [TestMethod]
    public async Task InMemorySecretStore_SaveReadDelete_RoundTripsSecretWithoutStableReferenceContent()
    {
        var store = new InMemorySecretStore();
        var request = new SecretStoreSaveRequest(
            "spotify",
            new Dictionary<string, string>
            {
                ["access_token"] = "access-token-value",
                ["refresh_token"] = "refresh-token-value",
            },
            DateTimeOffset.UtcNow.AddHours(1));

        var reference = await store.SaveAsync(request);
        var entry = await store.ReadAsync(reference);
        var deleted = await store.DeleteAsync(reference);
        var missing = await store.ReadAsync(reference);

        StringAssert.StartsWith(reference, "secret://memory/");
        Assert.IsFalse(reference.Contains("access-token-value", StringComparison.Ordinal));
        Assert.IsNotNull(entry);
        Assert.AreEqual("access-token-value", entry.Secrets["access_token"]);
        Assert.AreEqual("refresh-token-value", entry.Secrets["refresh_token"]);
        Assert.IsTrue(deleted);
        Assert.IsNull(missing);
    }

    [TestMethod]
    public async Task WindowsDpapiSecretStore_SaveReadDelete_DoesNotPersistPlaintextSecret()
    {
        if (!OperatingSystem.IsWindows())
            return;
        if (!WindowsDpapiSecretStore.IsAvailable())
            return;

        using var temp = TemporaryDirectory.Create();
        var store = new WindowsDpapiSecretStore(temp.Path);
        var request = new SecretStoreSaveRequest(
            "spotify",
            new Dictionary<string, string>
            {
                ["access_token"] = "plain-access-token",
                ["refresh_token"] = "plain-refresh-token",
            },
            DateTimeOffset.UtcNow.AddMinutes(30));

        var reference = await store.SaveAsync(request);
        var files = Directory.GetFiles(temp.Path, "*.secret");
        var fileText = Encoding.UTF8.GetString(await File.ReadAllBytesAsync(files.Single()));
        var entry = await store.ReadAsync(reference);
        var deleted = await store.DeleteAsync(reference);
        var deletedAgain = await store.DeleteAsync(reference);

        StringAssert.StartsWith(reference, "secret://windows-dpapi/");
        Assert.IsFalse(reference.Contains("plain-access-token", StringComparison.Ordinal));
        Assert.IsFalse(reference.Contains("plain-refresh-token", StringComparison.Ordinal));
        Assert.IsFalse(fileText.Contains("plain-access-token", StringComparison.Ordinal));
        Assert.IsFalse(fileText.Contains("plain-refresh-token", StringComparison.Ordinal));
        Assert.IsNotNull(entry);
        Assert.AreEqual("plain-access-token", entry.Secrets["access_token"]);
        Assert.AreEqual("plain-refresh-token", entry.Secrets["refresh_token"]);
        Assert.IsTrue(deleted);
        Assert.IsFalse(deletedAgain);
        Assert.AreEqual(0, Directory.GetFiles(temp.Path, "*.secret").Length);
    }

    [TestMethod]
    public async Task LocalEncryptedFileSecretStore_SaveReadDelete_DoesNotPersistPlaintextSecret()
    {
        using var temp = TemporaryDirectory.Create();
        var store = new LocalEncryptedFileSecretStore(temp.Path);
        var request = new SecretStoreSaveRequest(
            "soulseek",
            new Dictionary<string, string>
            {
                ["password"] = "plain-soulseek-password",
            });

        var reference = await store.SaveAsync(request);
        var files = Directory.GetFiles(temp.Path, "*.secret");
        byte[] secretBytes = await File.ReadAllBytesAsync(files.Single());
        byte[] keyBytes = await File.ReadAllBytesAsync(Path.Combine(temp.Path, "master.key"));
        var entry = await store.ReadAsync(reference);
        var deleted = await store.DeleteAsync(reference);

        StringAssert.StartsWith(reference, "secret://local-encrypted/");
        Assert.IsFalse(Encoding.UTF8.GetString(secretBytes).Contains("plain-soulseek-password", StringComparison.Ordinal));
        Assert.AreEqual(32, keyBytes.Length);
        CollectionAssert.AreNotEqual(keyBytes, secretBytes);
        Assert.IsNotNull(entry);
        Assert.AreEqual("plain-soulseek-password", entry.Secrets["password"]);
        Assert.IsTrue(deleted);
        Assert.IsFalse(File.Exists(files.Single()));
        if (!OperatingSystem.IsWindows())
        {
            Assert.AreEqual(
                UnixFileMode.UserRead | UnixFileMode.UserWrite,
                File.GetUnixFileMode(Path.Combine(temp.Path, "master.key")));
            Assert.AreEqual(
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                File.GetUnixFileMode(temp.Path));
        }
    }

    [TestMethod]
    public async Task LocalEncryptedFileSecretStore_TamperedPayload_FailsAuthentication()
    {
        using var temp = TemporaryDirectory.Create();
        var store = new LocalEncryptedFileSecretStore(temp.Path);
        string reference = await store.SaveAsync(new SecretStoreSaveRequest(
            "soulseek",
            new Dictionary<string, string> { ["password"] = "secret" }));
        string path = Directory.GetFiles(temp.Path, "*.secret").Single();
        byte[] payload = await File.ReadAllBytesAsync(path);
        var envelope = JsonNode.Parse(payload)!.AsObject();
        byte[] ciphertext = Convert.FromBase64String(envelope["Ciphertext"]!.GetValue<string>());
        ciphertext[0] ^= 0x01;
        envelope["Ciphertext"] = Convert.ToBase64String(ciphertext);
        await File.WriteAllTextAsync(path, envelope.ToJsonString());

        try
        {
            await store.ReadAsync(reference);
            Assert.Fail("Tampered secret payload should fail authentication.");
        }
        catch (InvalidDataException)
        {
        }
    }

    [TestMethod]
    public void PlatformSecretStoreFactory_Create_SelectsSupportedPersistentStore()
    {
        using var temp = TemporaryDirectory.Create();

        var store = PlatformSecretStoreFactory.Create(temp.Path);

        if (OperatingSystem.IsWindows() && WindowsDpapiSecretStore.IsAvailable())
            Assert.IsInstanceOfType<WindowsDpapiSecretStore>(store);
        else
            Assert.IsInstanceOfType<LocalEncryptedFileSecretStore>(store);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
            => Path = path;

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sockseek-secret-store-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
