using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Domain.Accounts;
using Sockseek.Domain.Playlists;
using Sockseek.Domain.Tracks;
using Sockseek.Infrastructure.Persistence;
using Sockseek.Infrastructure.Persistence.Entities;

namespace Sockseek.Infrastructure.Tests.Persistence;

[TestClass]
public sealed class DesktopVirtualizationTraceFixtureWriterTests
{
    private const string EnabledEnvVar = "SOCKSEEK_WRITE_DESKTOP_TRACE_FIXTURE";
    private const string ConfigDirEnvVar = "SOCKSEEK_DESKTOP_TRACE_FIXTURE_CONFIG_DIR";
    private const string OverwriteEnvVar = "SOCKSEEK_DESKTOP_TRACE_FIXTURE_OVERWRITE";
    private const string LibraryCountEnvVar = "SOCKSEEK_DESKTOP_TRACE_LIBRARY_COUNT";
    private const string PlaylistCountEnvVar = "SOCKSEEK_DESKTOP_TRACE_PLAYLIST_COUNT";
    private const int DefaultLibraryTrackCount = 100_000;
    private const int DefaultPlaylistItemCount = 10_000;
    private const int BatchSize = 1_000;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("Performance")]
    [Timeout(600_000)]
    public async Task WriteFixtureDatabase_WhenExplicitlyEnabled()
    {
        if (!IsEnabled(EnabledEnvVar))
        {
            TestContext.WriteLine(
                $"Skipped desktop trace fixture writer. Set {EnabledEnvVar}=1 to create an isolated fixture database.");
            return;
        }

        var configDir = Environment.GetEnvironmentVariable(ConfigDirEnvVar);
        if (string.IsNullOrWhiteSpace(configDir))
            throw new InvalidOperationException($"{ConfigDirEnvVar} is required when {EnabledEnvVar}=1.");

        var fullConfigDir = Path.GetFullPath(configDir);
        var databasePath = Path.Combine(fullConfigDir, "sockseek.db");
        if (File.Exists(databasePath) && !IsEnabled(OverwriteEnvVar))
            throw new InvalidOperationException(
                $"Fixture database already exists: {databasePath}. Set {OverwriteEnvVar}=1 to replace it.");

        Directory.CreateDirectory(fullConfigDir);
        if (File.Exists(databasePath))
            File.Delete(databasePath);

        var libraryTrackCount = ReadPositiveInt(LibraryCountEnvVar, DefaultLibraryTrackCount);
        var playlistItemCount = ReadPositiveInt(PlaylistCountEnvVar, DefaultPlaylistItemCount);
        var options = new DbContextOptionsBuilder<SockseekDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        await using (var setup = new SockseekDbContext(options))
        {
            await setup.Database.MigrateAsync();
        }

        await SeedLibraryAsync(options, libraryTrackCount);
        Guid playlistId;
        await using (var context = new SockseekDbContext(options))
        {
            playlistId = await new ExternalPlaylistSnapshotStore(context)
                .UpsertAsync(CreateLargePlaylistSnapshot(playlistItemCount));
        }

        TestContext.WriteLine($"Wrote desktop trace fixture database: {databasePath}");
        TestContext.WriteLine($"Library tracks: {libraryTrackCount}");
        TestContext.WriteLine($"Playlist items: {playlistItemCount}");
        TestContext.WriteLine($"Playlist id: {playlistId}");
    }

    private static async Task SeedLibraryAsync(DbContextOptions<SockseekDbContext> options, int count)
    {
        var timestamp = new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
        for (int offset = 0; offset < count; offset += BatchSize)
        {
            await using var context = new SockseekDbContext(options);
            var limit = Math.Min(BatchSize, count - offset);
            for (int index = offset; index < offset + limit; index++)
            {
                var artist = $"Trace Artist {index % 1000:D4}";
                var title = $"Trace Library Track {index:D6}";
                var track = new CanonicalTrackEntity
                {
                    Id = Guid.NewGuid(),
                    Artist = artist,
                    Title = title,
                    AlbumTitle = $"Trace Album {index / 12:D5}",
                    DurationMs = 180000 + index,
                    NormalizedArtist = NormalizeForMatch(artist),
                    NormalizedTitle = NormalizeForMatch(title),
                };
                track.LocalMediaFiles.Add(new LocalMediaFileEntity
                {
                    Id = Guid.NewGuid(),
                    Path = $"C:/SockseekTrace/Library/Artist {index % 1000:D4}/Track {index:D6}.mp3",
                    Size = 4096 + index,
                    LastWriteUtc = timestamp,
                    DurationMs = track.DurationMs,
                    Codec = "mp3",
                    Bitrate = 320,
                    SampleRate = 44100,
                    BitDepth = 16,
                    Availability = (int)LocalMediaAvailability.Available,
                });
                context.CanonicalTracks.Add(track);
            }

            await context.SaveChangesAsync();
        }
    }

    private static ExternalPlaylistSnapshotRecord CreateLargePlaylistSnapshot(int itemCount)
        => new(
            ExternalProvider.Spotify,
            "desktop-trace-playlist",
            "Desktop Trace Playlist",
            "https://example.test/sockseek/desktop-trace-playlist",
            1,
            new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero),
            PlaylistImportMode.Mirror,
            "Desktop Trace Playlist",
            Enumerable.Range(1, itemCount)
                .Select(index => new ExternalPlaylistItemSnapshot(
                    $"trace-item-{index:D5}",
                    index,
                    $"Trace Playlist Track {index:D5}",
                    "Trace Playlist Artist",
                    "Trace Playlist Album",
                    180000 + index,
                    ExternalTrackId: $"trace-track-{index:D5}",
                    Isrc: null,
                    ExternalUrl: $"https://example.test/sockseek/trace-track-{index:D5}",
                    ArtworkUrl: null,
                    MusicBrainzRecordingId: null,
                    Artists: null))
                .ToArray(),
            new ExternalAccountRecord(
                ExternalProvider.Spotify,
                "desktop-trace-user",
                "Desktop Trace User",
                "secret://spotify/desktop-trace",
                new DateTimeOffset(2026, 10, 13, 11, 55, 0, TimeSpan.Zero)));

    private static int ReadPositiveInt(string variable, int defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        if (int.TryParse(value.Trim(), out var parsed) && parsed > 0)
            return parsed;

        throw new InvalidOperationException($"{variable} must be a positive integer.");
    }

    private static bool IsEnabled(string variable)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeForMatch(string value)
    {
        var chars = value.Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : ' ')
            .ToArray();

        return string.Join(' ', new string(chars)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
