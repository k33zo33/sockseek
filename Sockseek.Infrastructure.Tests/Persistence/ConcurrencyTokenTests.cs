using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Infrastructure.Persistence;
using Sockseek.Infrastructure.Persistence.Entities;

namespace Sockseek.Infrastructure.Tests.Persistence;

[TestClass]
public class ConcurrencyTokenTests
{
    [TestMethod]
    public async Task SaveChangesAsync_ConcurrentPlaylistUpdate_ThrowsDbUpdateConcurrencyException()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SockseekDbContext>()
            .UseSqlite(connection)
            .Options;

        Guid playlistId;
        await using (var setup = new SockseekDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            var playlist = new PlaylistEntity
            {
                Id = Guid.NewGuid(),
                Name = "Morning Mix",
                ImportMode = 1,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            };
            setup.Playlists.Add(playlist);
            await setup.SaveChangesAsync();
            playlistId = playlist.Id;
        }

        await using var first = new SockseekDbContext(options);
        await using var second = new SockseekDbContext(options);

        var firstPlaylist = await first.Playlists.SingleAsync(x => x.Id == playlistId);
        var secondPlaylist = await second.Playlists.SingleAsync(x => x.Id == playlistId);

        firstPlaylist.Name = "Morning Mix Updated";
        await first.SaveChangesAsync();

        secondPlaylist.Name = "Morning Mix Conflicting Edit";
        await Assert.ThrowsExceptionAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [TestMethod]
    public async Task EnsureCreated_AddsConcurrencyTokenToMutableTables()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SockseekDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new SockseekDbContext(options);
        await context.Database.EnsureCreatedAsync();

        foreach (var table in new[]
                 {
                     "ExternalAccounts",
                     "ExternalPlaylists",
                     "Playlists",
                     "PlaylistItems",
                     "CanonicalTracks",
                     "LocalMediaFiles",
                     "DownloadWorkflows",
                     "ProviderSyncStates",
                     "AppSettings",
                     "SchemaInfo"
                 })
        {
            var columns = await ColumnNamesAsync(connection, table);
            CollectionAssert.Contains(columns, "ConcurrencyToken");
        }
    }

    private static async Task<string[]> ColumnNamesAsync(SqliteConnection connection, string table)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info([{table}]);";
        var columns = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            columns.Add(reader.GetString(1));

        return columns.ToArray();
    }
}
