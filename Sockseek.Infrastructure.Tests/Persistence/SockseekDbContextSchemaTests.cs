using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Infrastructure.Persistence;

namespace Sockseek.Infrastructure.Tests.Persistence;

[TestClass]
public class SockseekDbContextSchemaTests
{
    [TestMethod]
    public async Task EnsureCreated_CreatesExpectedTablesAndUniqueIndexes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SockseekDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new SockseekDbContext(options);
        await context.Database.EnsureCreatedAsync();

        CollectionAssert.IsSubsetOf(
            new[]
            {
                "AppProfiles",
                "ExternalAccounts",
                "ExternalPlaylists",
                "Playlists",
                "PlaylistItems",
                "CanonicalTracks",
                "TrackSources",
                "LocalMediaFiles",
                "ResolutionAttempts",
                "DownloadWorkflows",
                "ProviderSyncStates",
                "AppSettings",
                "SchemaInfo",
            },
            await TableNamesAsync(connection));

        var indexColumns = await UniqueIndexColumnsByTableAsync(connection);
        CollectionAssert.Contains(indexColumns["ExternalAccounts"], "Provider,ExternalUserId");
        CollectionAssert.Contains(indexColumns["ExternalPlaylists"], "Provider,ExternalId,AccountId");
        CollectionAssert.Contains(indexColumns["PlaylistItems"], "PlaylistId,ProviderItemId");
        CollectionAssert.Contains(indexColumns["TrackSources"], "Provider,ExternalId");
        CollectionAssert.Contains(indexColumns["LocalMediaFiles"], "Path");
        CollectionAssert.Contains(indexColumns["ProviderSyncStates"], "Provider,AccountId,ResourceId");
        CollectionAssert.Contains(indexColumns["DownloadWorkflows"], "WorkflowId");
    }

    [TestMethod]
    public async Task ExternalAccountsSchema_DoesNotContainTokenColumns()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SockseekDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new SockseekDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var columns = await ColumnNamesAsync(connection, "ExternalAccounts");
        CollectionAssert.Contains(columns, "SecretReference");
        Assert.IsFalse(columns.Any(name => name.Contains("token", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(name, "ConcurrencyToken", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(columns.Any(name => name.Contains("refresh", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(columns.Any(name => name.Contains("oauth", StringComparison.OrdinalIgnoreCase)));
    }

    private static async Task<string[]> TableNamesAsync(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            names.Add(reader.GetString(0));

        return names.ToArray();
    }

    private static async Task<Dictionary<string, List<string>>> UniqueIndexColumnsByTableAsync(SqliteConnection connection)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var table in await TableNamesAsync(connection))
        {
            var list = new List<string>();
            using var indexCommand = connection.CreateCommand();
            indexCommand.CommandText = $"PRAGMA index_list([{table}]);";
            await using var indexReader = await indexCommand.ExecuteReaderAsync();
            var indexes = new List<string>();
            while (await indexReader.ReadAsync())
            {
                bool unique = indexReader.GetInt64(2) != 0;
                if (unique)
                    indexes.Add(indexReader.GetString(1));
            }

            foreach (var index in indexes)
            {
                using var infoCommand = connection.CreateCommand();
                infoCommand.CommandText = $"PRAGMA index_info([{index}]);";
                await using var infoReader = await infoCommand.ExecuteReaderAsync();
                var columns = new List<string>();
                while (await infoReader.ReadAsync())
                    columns.Add(infoReader.GetString(2));

                list.Add(string.Join(',', columns));
            }

            result[table] = list;
        }

        return result;
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
