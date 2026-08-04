using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Domain.Accounts;
using Sockseek.Domain.Common;
using Sockseek.Domain.Playlists;

namespace Sockseek.Domain.Tests.Playlists;

[TestClass]
public class ExternalPlaylistTests
{
    [TestMethod]
    public void Constructor_AllowsPublicPlaylistWithoutLinkedAccount()
    {
        var syncedAt = new DateTimeOffset(2026, 8, 4, 18, 0, 0, TimeSpan.Zero);

        var playlist = new ExternalPlaylist(
            ExternalProvider.Bandcamp,
            "playlist-1",
            "Wishlist",
            " https://example.test/list ",
            accountId: null,
            snapshotVersion: 3,
            syncedAt);

        Assert.AreEqual(ExternalProvider.Bandcamp, playlist.Provider);
        Assert.AreEqual("playlist-1", playlist.ExternalId);
        Assert.IsNull(playlist.AccountId);
        Assert.AreEqual("https://example.test/list", playlist.Url);
        Assert.AreEqual(3, playlist.SnapshotVersion);
    }

    [TestMethod]
    public void ApplySnapshot_UpdatesMetadata_AndNeverMovesVersionBackward()
    {
        var accountId = EntityId.New();
        var playlist = new ExternalPlaylist(
            ExternalProvider.Spotify,
            "playlist-1",
            "Daily Mix",
            null,
            accountId,
            snapshotVersion: 5,
            new DateTimeOffset(2026, 8, 4, 18, 0, 0, TimeSpan.Zero));

        playlist.ApplySnapshot(
            "Daily Mix Updated",
            "https://example.test/mix",
            snapshotVersion: 4,
            syncedAtUtc: new DateTimeOffset(2026, 8, 4, 18, 30, 0, TimeSpan.Zero));

        Assert.AreEqual("Daily Mix Updated", playlist.Name);
        Assert.AreEqual("https://example.test/mix", playlist.Url);
        Assert.AreEqual(5, playlist.SnapshotVersion);
        Assert.AreEqual(new DateTimeOffset(2026, 8, 4, 18, 30, 0, TimeSpan.Zero), playlist.LastSyncedAtUtc);
    }
}
