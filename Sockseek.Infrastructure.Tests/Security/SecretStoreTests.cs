using System.Text;
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
