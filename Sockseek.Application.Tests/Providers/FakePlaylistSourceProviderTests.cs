using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Application.Providers;
using Sockseek.Application.Security;
using Sockseek.Integrations.Abstractions;
using Sockseek.Integrations.Fake;

namespace Tests.Application.Providers;

[TestClass]
public sealed class FakePlaylistSourceProviderTests
{
    [TestMethod]
    public async Task FakeProvider_ConnectsImportsSyncsAndDisconnects()
    {
        var secretStore = new RecordingSecretStore();
        var provider = new FakePlaylistSourceProvider(secretStore);
        provider.UpsertPlaylist(new FakePlaylistSeed(
            "playlist-1",
            "Fake Mix",
            "https://fake.example/playlist-1",
            [
                new FakeTrackSeed("track-1", "item-1", "First", ["Artist"], DurationMs: 180000),
                new FakeTrackSeed("track-2", "item-2", "Second", ["Artist"], DurationMs: 200000),
            ]));

        var start = await provider.StartAuthorizationAsync(new AuthorizationRequest(
            ProviderIds.Fake,
            new Uri("http://127.0.0.1:49200/callback"),
            ["fake.playlists"],
            "state-1",
            "challenge-1",
            OAuthPkceCoordinator.CodeChallengeMethod), CancellationToken.None);
        var account = await provider.CompleteAuthorizationAsync(new AuthorizationCallback(
            ProviderIds.Fake,
            new Uri("http://127.0.0.1:49200/callback"),
            start.State,
            "fake-code",
            null), CancellationToken.None);

        var summaries = await provider.GetPlaylistsAsync(account.AccountId, CancellationToken.None);
        var firstSnapshot = await provider.GetPlaylistAsync(new ExternalPlaylistRequest(
            account.AccountId,
            ProviderIds.Fake,
            "playlist-1",
            null), CancellationToken.None);

        provider.UpsertPlaylist(new FakePlaylistSeed(
            "playlist-1",
            "Fake Mix",
            "https://fake.example/playlist-1",
            [
                new FakeTrackSeed("track-1", "item-1", "First", ["Artist"], DurationMs: 180000),
                new FakeTrackSeed("track-3", "item-3", "Third", ["Artist"], DurationMs: 210000),
            ]));
        var syncedSnapshot = await provider.GetPlaylistAsync(new ExternalPlaylistRequest(
            account.AccountId,
            ProviderIds.Fake,
            "playlist-1",
            null), CancellationToken.None);

        await provider.DisconnectAsync(account.AccountId, CancellationToken.None);

        Assert.AreEqual(ProviderIds.Fake, provider.ProviderId);
        Assert.AreEqual(1, summaries.Count);
        Assert.AreEqual(2, summaries.Single().ItemCount);
        Assert.AreEqual("secret://test/1", account.SecretReference);
        Assert.IsFalse(secretStore.DeletedBeforeSave);
        CollectionAssert.AreEquivalent(
            new[] { "access_token", "refresh_token" },
            secretStore.SavedSecrets.Keys.ToArray());
        Assert.AreEqual(2, firstSnapshot.Items.Count);
        Assert.AreEqual("First", firstSnapshot.Items[0].Title);
        Assert.AreEqual(2, syncedSnapshot.Items.Count);
        Assert.AreEqual("Third", syncedSnapshot.Items[1].Title);
        Assert.IsTrue(syncedSnapshot.SnapshotVersion > firstSnapshot.SnapshotVersion);
        Assert.IsTrue(secretStore.DeletedReferences.Contains(account.SecretReference));
    }

    private sealed class RecordingSecretStore : ISecretStore
    {
        private int nextId = 1;

        public Dictionary<string, string> SavedSecrets { get; } = new(StringComparer.Ordinal);

        public List<string> DeletedReferences { get; } = [];

        public bool DeletedBeforeSave { get; private set; }

        public Task<string> SaveAsync(SecretStoreSaveRequest request, CancellationToken cancellationToken = default)
        {
            foreach (var (key, value) in request.Secrets)
                SavedSecrets[key] = value;

            return Task.FromResult("secret://test/" + nextId++);
        }

        public Task<SecretStoreEntry?> ReadAsync(string secretReference, CancellationToken cancellationToken = default)
            => Task.FromResult<SecretStoreEntry?>(new SecretStoreEntry(secretReference, SavedSecrets, null));

        public Task<bool> DeleteAsync(string secretReference, CancellationToken cancellationToken = default)
        {
            if (SavedSecrets.Count == 0)
                DeletedBeforeSave = true;

            DeletedReferences.Add(secretReference);
            return Task.FromResult(true);
        }
    }
}
