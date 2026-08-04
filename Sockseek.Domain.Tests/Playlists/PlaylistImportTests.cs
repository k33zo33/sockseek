using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Domain.Playlists;

namespace Sockseek.Domain.Tests.Playlists;

[TestClass]
public class PlaylistImportTests
{
    [TestMethod]
    public void ApplyImportSnapshot_RepeatedSnapshot_DoesNotDuplicateItems()
    {
        var now = new DateTimeOffset(2026, 8, 4, 18, 0, 0, TimeSpan.Zero);
        var playlist = new Playlist("Daily Mix", PlaylistImportMode.Mirror, now);
        var snapshot = new[]
        {
            new ExternalPlaylistItemSnapshot("item-1", 1, "Track One", "Artist", "Album", 180000),
            new ExternalPlaylistItemSnapshot("item-2", 2, "Track Two", "Artist", "Album", 181000),
        };

        playlist.ApplyImportSnapshot(snapshot, now);
        playlist.ApplyImportSnapshot(snapshot, now.AddMinutes(5));

        Assert.AreEqual(2, playlist.Items.Count);
        CollectionAssert.AreEqual(new[] { "item-1", "item-2" }, playlist.Items.Select(item => item.ProviderItemId).ToArray());
        Assert.IsTrue(playlist.Items.All(item => item.Status == PlaylistItemStatus.Imported));
    }

    [TestMethod]
    public void ApplyImportSnapshot_MirrorMode_MarksMissingItemsAsRemoved()
    {
        var now = new DateTimeOffset(2026, 8, 4, 18, 0, 0, TimeSpan.Zero);
        var playlist = new Playlist("Mirror Import", PlaylistImportMode.Mirror, now);

        playlist.ApplyImportSnapshot(
        [
            new ExternalPlaylistItemSnapshot("item-1", 1, "Track One", "Artist", "Album", 180000),
            new ExternalPlaylistItemSnapshot("item-2", 2, "Track Two", "Artist", "Album", 181000),
        ],
        now);

        playlist.ApplyImportSnapshot(
        [
            new ExternalPlaylistItemSnapshot("item-1", 1, "Track One", "Artist", "Album", 180000),
        ],
        now.AddMinutes(10));

        var removed = playlist.Items.Single(item => item.ProviderItemId == "item-2");
        Assert.AreEqual(PlaylistItemStatus.RemovedFromSourcePlaylist, removed.Status);
        Assert.AreEqual(now.AddMinutes(10), removed.RemovedAtUtc);
    }

    [TestMethod]
    public void ApplyImportSnapshot_CopyMode_KeepsMissingItemsAvailableForLocalUse()
    {
        var now = new DateTimeOffset(2026, 8, 4, 18, 0, 0, TimeSpan.Zero);
        var playlist = new Playlist("Copy Import", PlaylistImportMode.Copy, now);

        playlist.ApplyImportSnapshot(
        [
            new ExternalPlaylistItemSnapshot("item-1", 1, "Track One", "Artist", "Album", 180000),
            new ExternalPlaylistItemSnapshot("item-2", 2, "Track Two", "Artist", "Album", 181000),
        ],
        now);

        playlist.ApplyImportSnapshot(
        [
            new ExternalPlaylistItemSnapshot("item-1", 1, "Track One", "Artist", "Album", 180000),
        ],
        now.AddMinutes(10));

        var retained = playlist.Items.Single(item => item.ProviderItemId == "item-2");
        Assert.AreEqual(PlaylistItemStatus.Imported, retained.Status);
        Assert.IsNull(retained.RemovedAtUtc);
    }

    [TestMethod]
    public void ApplyImportSnapshot_ReintroducedMirrorItem_ClearsRemovedStatus()
    {
        var now = new DateTimeOffset(2026, 8, 4, 18, 0, 0, TimeSpan.Zero);
        var playlist = new Playlist("Mirror Import", PlaylistImportMode.Mirror, now);

        playlist.ApplyImportSnapshot(
        [new ExternalPlaylistItemSnapshot("item-1", 1, "Track One", "Artist", "Album", 180000)],
        now);
        playlist.ApplyImportSnapshot([], now.AddMinutes(10));
        playlist.ApplyImportSnapshot(
        [new ExternalPlaylistItemSnapshot("item-1", 1, "Track One", "Artist", "Album", 180000)],
        now.AddMinutes(20));

        var item = playlist.Items.Single();
        Assert.AreEqual(PlaylistItemStatus.Imported, item.Status);
        Assert.IsNull(item.RemovedAtUtc);
    }
}
