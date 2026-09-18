using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Application.Common;
using Sockseek.Domain.Tracks;
using Sockseek.Infrastructure.Persistence;

namespace Sockseek.Infrastructure.Tests.Persistence;

[TestClass]
public sealed class PlaybackQueueStoreTests
{
    [TestMethod]
    public async Task SaveAsync_ThenGetAsync_RestoresQueueStateAndItemsInOrder()
    {
        await using var database = await TestDatabase.CreateAsync();
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 18, 20, 30, 0, TimeSpan.Zero));
        var queueId = Guid.NewGuid();
        Guid trackId;
        Guid fileId;

        await using (var context = new SockseekDbContext(database.Options))
        {
            trackId = await new CanonicalTrackStore(context).UpsertAsync(CreateTrack("Artist", "Track"));
            fileId = await context.LocalMediaFiles.Select(file => file.Id).SingleAsync();

            await new PlaybackQueueStore(context, clock).SaveAsync(new PlaybackQueueSaveRecord(
                queueId,
                "Main queue",
                CurrentIndex: 1,
                PlaybackQueueRepeatMode.All,
                ShuffleSeed: 12345,
                [
                    new PlaybackQueueItemRecord(Guid.NewGuid(), 1, trackId, fileId, null, PlaybackQueueItemState.LocalFile),
                    new PlaybackQueueItemRecord(Guid.NewGuid(), 0, trackId, fileId, null, PlaybackQueueItemState.LocalFile),
                ]));
        }

        await using var verify = new SockseekDbContext(database.Options);
        var restored = await new PlaybackQueueStore(verify, clock).GetAsync(queueId);

        Assert.IsNotNull(restored);
        Assert.AreEqual("Main queue", restored.Name);
        Assert.AreEqual(1, restored.CurrentIndex);
        Assert.AreEqual(PlaybackQueueRepeatMode.All, restored.RepeatMode);
        Assert.AreEqual(12345, restored.ShuffleSeed);
        Assert.AreEqual(clock.UtcNow, restored.UpdatedAtUtc);
        Assert.AreEqual(2, restored.Items.Count);
        CollectionAssert.AreEqual(new[] { 0, 1 }, restored.Items.Select(item => item.Position).ToArray());
        Assert.IsTrue(restored.Items.All(item => item.CanonicalTrackId == trackId));
        Assert.IsTrue(restored.Items.All(item => item.LocalMediaFileId == fileId));
    }

    [TestMethod]
    public async Task SaveAsync_ExistingQueue_ReplacesItems()
    {
        await using var database = await TestDatabase.CreateAsync();
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 18, 20, 35, 0, TimeSpan.Zero));
        var queueId = Guid.NewGuid();
        Guid firstTrackId;
        Guid secondTrackId;

        await using (var context = new SockseekDbContext(database.Options))
        {
            var trackStore = new CanonicalTrackStore(context);
            firstTrackId = await trackStore.UpsertAsync(CreateTrack("Artist", "First"));
            secondTrackId = await trackStore.UpsertAsync(CreateTrack("Artist", "Second"));
        }

        await using (var context = new SockseekDbContext(database.Options))
        {
            var store = new PlaybackQueueStore(context, clock);
            await store.SaveAsync(new PlaybackQueueSaveRecord(
                queueId,
                "Main queue",
                0,
                PlaybackQueueRepeatMode.None,
                7,
                [new PlaybackQueueItemRecord(Guid.NewGuid(), 0, firstTrackId, null, null, PlaybackQueueItemState.PendingResolution)]));

            clock.UtcNow = clock.UtcNow.AddMinutes(1);
            await store.SaveAsync(new PlaybackQueueSaveRecord(
                queueId,
                "Updated queue",
                0,
                PlaybackQueueRepeatMode.One,
                99,
                [new PlaybackQueueItemRecord(Guid.NewGuid(), 0, secondTrackId, null, null, PlaybackQueueItemState.PendingResolution)]));
        }

        await using var verify = new SockseekDbContext(database.Options);
        var restored = await new PlaybackQueueStore(verify, clock).GetAsync(queueId);

        Assert.IsNotNull(restored);
        Assert.AreEqual("Updated queue", restored.Name);
        Assert.AreEqual(PlaybackQueueRepeatMode.One, restored.RepeatMode);
        Assert.AreEqual(99, restored.ShuffleSeed);
        Assert.AreEqual(1, restored.Items.Count);
        Assert.AreEqual(secondTrackId, restored.Items.Single().CanonicalTrackId);
        Assert.AreEqual(1, await verify.PlaybackQueueItems.CountAsync());
    }

    [TestMethod]
    public async Task SaveAsync_NonContiguousPositions_Throws()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = new SockseekDbContext(database.Options);
        var trackId = await new CanonicalTrackStore(context).UpsertAsync(CreateTrack("Artist", "Track"));
        var store = new PlaybackQueueStore(context, new FakeClock(DateTimeOffset.UtcNow));

        await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
            store.SaveAsync(new PlaybackQueueSaveRecord(
                Guid.NewGuid(),
                "Main queue",
                0,
                PlaybackQueueRepeatMode.None,
                1,
                [new PlaybackQueueItemRecord(Guid.NewGuid(), 2, trackId, null, null, PlaybackQueueItemState.PendingResolution)])));
    }

    private static CanonicalTrackRecord CreateTrack(string artist, string title)
        => new(
            artist,
            title,
            null,
            180000,
            null,
            null,
            [],
            [
                new LocalMediaFileRecord(
                    $"C:/Music/{artist}/{title}.mp3",
                    1234,
                    new DateTimeOffset(2026, 9, 18, 20, 0, 0, TimeSpan.Zero),
                    180000,
                    "mp3",
                    320,
                    44100,
                    16,
                    LocalMediaAvailability.Available)
            ]);

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
    }

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
