using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Application.Playback;
using Sockseek.Domain.Accounts;
using Sockseek.Domain.Playlists;
using Sockseek.Domain.Tracks;
using Sockseek.Infrastructure.Persistence;

namespace Sockseek.Infrastructure.Tests.Persistence;

[TestClass]
public sealed class LocalPlaybackSourceResolverTests
{
    [TestMethod]
    public async Task ResolveCanonicalTrackAsync_ReturnsBestAvailableLocalFile()
    {
        await using var database = await TestDatabase.CreateAsync();
        Guid trackId;

        await using (var context = new SockseekDbContext(database.Options))
        {
            trackId = await new CanonicalTrackStore(context).UpsertAsync(new CanonicalTrackRecord(
                "Artist",
                "Track",
                "Album",
                180000,
                null,
                null,
                [],
                [
                    CreateFile("C:/Music/Track-128.mp3", LocalMediaAvailability.Available, bitrate: 128),
                    CreateFile("C:/Music/Track.flac", LocalMediaAvailability.Available, bitrate: 900),
                ]));
        }

        await using var resolveContext = new SockseekDbContext(database.Options);
        var result = await new LocalPlaybackSourceResolver(resolveContext).ResolveCanonicalTrackAsync(trackId);

        Assert.AreEqual(PlaybackSourceKind.LocalFile, result.Kind);
        Assert.AreEqual(trackId, result.CanonicalTrackId);
        Assert.AreEqual("C:/Music/Track.flac", result.Path);
        Assert.IsNotNull(result.LocalMediaFileId);
    }

    [TestMethod]
    public async Task ResolveCanonicalTrackAsync_WithoutAvailableFile_ReturnsUnavailable()
    {
        await using var database = await TestDatabase.CreateAsync();
        Guid trackId;

        await using (var context = new SockseekDbContext(database.Options))
        {
            trackId = await new CanonicalTrackStore(context).UpsertAsync(new CanonicalTrackRecord(
                "Artist",
                "Track",
                null,
                180000,
                null,
                null,
                [],
                [CreateFile("C:/Music/Missing.mp3", LocalMediaAvailability.Missing, bitrate: 320)]));
        }

        await using var resolveContext = new SockseekDbContext(database.Options);
        var result = await new LocalPlaybackSourceResolver(resolveContext).ResolveCanonicalTrackAsync(trackId);

        Assert.AreEqual(PlaybackSourceKind.Unavailable, result.Kind);
        Assert.AreEqual(trackId, result.CanonicalTrackId);
        StringAssert.Contains(result.Reason, "No available local media file");
    }

    [TestMethod]
    public async Task ResolvePlaylistItemAsync_AvailableLocalItem_ReturnsLocalFile()
    {
        await using var database = await TestDatabase.CreateAsync();
        Guid playlistItemId;
        Guid trackId;

        await using (var context = new SockseekDbContext(database.Options))
        {
            playlistItemId = await CreatePlaylistItemAsync(context, PlaylistItemStatus.AvailableLocal);
            trackId = await new CanonicalTrackStore(context).UpsertAsync(new CanonicalTrackRecord(
                "Artist",
                "Track",
                null,
                180000,
                null,
                null,
                [],
                [CreateFile("C:/Music/Track.mp3", LocalMediaAvailability.Available, bitrate: 320)]));

            var item = await context.PlaylistItems.SingleAsync();
            item.CanonicalTrackId = trackId;
            await context.SaveChangesAsync();
        }

        await using var resolveContext = new SockseekDbContext(database.Options);
        var result = await new LocalPlaybackSourceResolver(resolveContext).ResolvePlaylistItemAsync(playlistItemId);

        Assert.AreEqual(PlaybackSourceKind.LocalFile, result.Kind);
        Assert.AreEqual(trackId, result.CanonicalTrackId);
        Assert.AreEqual(playlistItemId, result.PlaylistItemId);
        Assert.AreEqual("C:/Music/Track.mp3", result.Path);
    }

    [TestMethod]
    public async Task ResolvePlaylistItemAsync_UnresolvedItem_ReturnsPendingResolution()
    {
        await using var database = await TestDatabase.CreateAsync();
        Guid playlistItemId;

        await using (var context = new SockseekDbContext(database.Options))
        {
            playlistItemId = await CreatePlaylistItemAsync(context, PlaylistItemStatus.Unresolved);
        }

        await using var resolveContext = new SockseekDbContext(database.Options);
        var result = await new LocalPlaybackSourceResolver(resolveContext).ResolvePlaylistItemAsync(playlistItemId);

        Assert.AreEqual(PlaybackSourceKind.PendingResolution, result.Kind);
        Assert.AreEqual(playlistItemId, result.PlaylistItemId);
        StringAssert.Contains(result.Reason, "not resolved");
    }

    private static async Task<Guid> CreatePlaylistItemAsync(SockseekDbContext context, PlaylistItemStatus status)
    {
        var playlistId = await new ExternalPlaylistSnapshotStore(context).UpsertAsync(new ExternalPlaylistSnapshotRecord(
            ExternalProvider.Spotify,
            "playlist-1",
            "Daily Mix",
            "https://example.test/playlist/1",
            1,
            new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero),
            PlaylistImportMode.Copy,
            "Daily Mix",
            [new ExternalPlaylistItemSnapshot("item-1", 1, "Track", "Artist", "Album", 180000)],
            null));
        var item = await context.PlaylistItems.SingleAsync(candidate => candidate.PlaylistId == playlistId);
        item.Status = (int)status;
        await context.SaveChangesAsync();
        return item.Id;
    }

    private static LocalMediaFileRecord CreateFile(
        string path,
        LocalMediaAvailability availability,
        int bitrate)
        => new(
            path,
            1234,
            new DateTimeOffset(2026, 9, 18, 12, 1, 0, TimeSpan.Zero),
            180000,
            Path.GetExtension(path).TrimStart('.'),
            bitrate,
            44100,
            16,
            availability);

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private TestDatabase(SqliteConnection connection, DbContextOptions<SockseekDbContext> options)
        {
            this.connection = connection;
            Options = options;
        }

        public DbContextOptions<SockseekDbContext> Options { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<SockseekDbContext>()
                .UseSqlite(connection)
                .Options;

            await using var setup = new SockseekDbContext(options);
            await setup.Database.MigrateAsync();

            return new TestDatabase(connection, options);
        }

        public ValueTask DisposeAsync()
            => connection.DisposeAsync();
    }
}
