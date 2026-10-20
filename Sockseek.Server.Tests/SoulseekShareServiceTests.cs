using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Application.Common;
using Sockseek.Infrastructure.Persistence;
using Sockseek.Infrastructure.SoulseekSharing;

namespace Sockseek.Server.Tests;

[TestClass]
public sealed class SoulseekShareServiceTests
{
    [TestMethod]
    public async Task RebuildAsync_ProjectsPersistedRootsAndRemovalStopsSharingWithoutDeletingAudio()
    {
        await using var fixture = await Fixture.CreateAsync();
        string albumDirectory = Path.Combine(fixture.MusicRoot, "Artist", "Album");
        Directory.CreateDirectory(albumDirectory);
        string audioPath = Path.Combine(albumDirectory, "track.mp3");
        await File.WriteAllBytesAsync(audioPath, [1, 2, 3, 4, 5]);
        Guid rootId;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var store = new SoulseekShareRootStore(
                scope.ServiceProvider.GetRequiredService<SockseekDbContext>(),
                scope.ServiceProvider.GetRequiredService<IClock>());
            rootId = (await store.SaveAsync(null, fixture.MusicRoot, "Music", enabled: true)).Id;
        }

        int changed = 0;
        fixture.ShareService.SnapshotChanged += (_, _) => changed++;
        var first = await fixture.ShareService.RebuildAsync();

        Assert.AreEqual(1, first.Revision);
        Assert.AreEqual(3, first.DirectoryCount);
        Assert.AreEqual(1, first.FileCount);
        Assert.AreEqual("@@Music\\Artist\\Album\\track.mp3", first.Files.Single().RemoteFilename);
        Assert.IsFalse(first.Files.Single().RemoteFilename.Contains(fixture.MusicRoot, StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(fixture.ShareService.GetBuildResult().Roots.Single().Included);
        Assert.AreEqual(1, changed);

        fixture.ShareService.Invalidate();
        Assert.AreEqual(0, fixture.ShareService.GetSnapshot().FileCount);
        await Assert.ThrowsExceptionAsync<FileNotFoundException>(() =>
            fixture.ShareService.OpenReadAsync("@@Music\\Artist\\Album\\track.mp3", 0));

        first = await fixture.ShareService.RebuildAsync();
        Assert.AreEqual(3, first.Revision);
        Assert.AreEqual(1, first.FileCount);

        await using (var stream = await fixture.ShareService.OpenReadAsync(
            "@@Music/Artist/Album/track.mp3",
            offset: 2))
        {
            Assert.AreEqual(2, stream.Position);
            Assert.AreEqual(3, stream.ReadByte());
        }

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var store = new SoulseekShareRootStore(
                scope.ServiceProvider.GetRequiredService<SockseekDbContext>(),
                scope.ServiceProvider.GetRequiredService<IClock>());
            Assert.IsTrue(await store.RemoveAsync(rootId));
        }

        var second = await fixture.ShareService.RebuildAsync();

        Assert.AreEqual(4, second.Revision);
        Assert.AreEqual(0, second.DirectoryCount);
        Assert.AreEqual(0, second.FileCount);
        Assert.AreEqual(4, changed);
        Assert.IsTrue(File.Exists(audioPath), "Removing a share root must never delete local audio.");
        await Assert.ThrowsExceptionAsync<FileNotFoundException>(() =>
            fixture.ShareService.OpenReadAsync("@@Music\\Artist\\Album\\track.mp3", 0));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly string root;

        private Fixture(
            SqliteConnection connection,
            string root,
            ServiceProvider services,
            SoulseekShareService shareService)
        {
            this.connection = connection;
            this.root = root;
            Services = services;
            ShareService = shareService;
            MusicRoot = Path.Combine(root, "music");
        }

        public ServiceProvider Services { get; }

        public SoulseekShareService ShareService { get; }

        public string MusicRoot { get; }

        public static async Task<Fixture> CreateAsync()
        {
            string root = Path.Combine(Path.GetTempPath(), $"sockseek-share-service-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var services = new ServiceCollection();
            services.AddSingleton<IClock>(new FakeClock(
                new DateTimeOffset(2026, 10, 20, 14, 0, 0, TimeSpan.Zero)));
            services.AddDbContext<SockseekDbContext>(options => options.UseSqlite(connection));
            var provider = services.BuildServiceProvider();
            await using (var scope = provider.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<SockseekDbContext>()
                    .Database.MigrateAsync();
            }

            var shareService = new SoulseekShareService(
                provider.GetRequiredService<IServiceScopeFactory>(),
                new SoulseekShareSnapshotBuilder());
            return new Fixture(connection, root, provider, shareService);
        }

        public async ValueTask DisposeAsync()
        {
            ShareService.Dispose();
            await Services.DisposeAsync();
            await connection.DisposeAsync();
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
