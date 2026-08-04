using Microsoft.EntityFrameworkCore;
using Sockseek.Domain.Accounts;
using Sockseek.Domain.Playlists;
using Sockseek.Domain.Tracks;
using Sockseek.Infrastructure.Persistence.Entities;

namespace Sockseek.Infrastructure.Persistence;

public sealed class DevelopmentFixtureSeeder(SockseekDbContext dbContext)
{
    private static readonly Guid ProfileId = Guid.Parse("4d1f4b90-76cb-43b7-b17f-4990f44f968d");
    private static readonly Guid AccountId = Guid.Parse("73c9fc66-5470-449d-8345-27d0d89e372c");
    private static readonly Guid ExternalPlaylistId = Guid.Parse("c4e54f5d-fdbc-47ea-a3ea-14cc0a470e2f");
    private static readonly Guid PlaylistId = Guid.Parse("fd21816b-58ec-4ff0-af8f-2f250e39ae44");
    private static readonly Guid TrackId = Guid.Parse("20a5eb64-c3db-4680-b306-d4fecbfbf7fd");
    private static readonly Guid TrackSourceId = Guid.Parse("d9c05f73-2cdf-41c9-b5a6-f6d2429006a7");
    private static readonly Guid MediaFileId = Guid.Parse("0c5a10a2-d421-4103-af32-ec08d31c35f9");
    private static readonly Guid PlaylistItemId = Guid.Parse("769e51dc-51ca-4d63-b19f-1b8053640cc0");
    private static readonly Guid SchemaInfoId = Guid.Parse("f0cc6e61-144d-4fef-a017-f0f66f88d1ba");

    public async Task<DevelopmentSeedResult> SeedAsync(bool isDevelopment, CancellationToken cancellationToken = default)
    {
        if (!isDevelopment)
            return new DevelopmentSeedResult(false, 0);

        if (await dbContext.AppProfiles.AnyAsync(cancellationToken))
            return new DevelopmentSeedResult(false, 0);

        var now = DateTimeOffset.UtcNow;

        var profile = new AppProfileEntity
        {
            Id = ProfileId,
            Name = "Development Fixture Profile",
            CreatedAtUtc = now,
            Active = true,
        };

        var account = new ExternalAccountEntity
        {
            Id = AccountId,
            Provider = (int)ExternalProvider.Spotify,
            ExternalUserId = "fixture-user",
            DisplayName = "Fixture User",
            SecretReference = "secret://fixtures/spotify/main",
            Status = (int)ExternalAccountStatus.Authorized,
            LastAuthorizedAtUtc = now,
        };

        var externalPlaylist = new ExternalPlaylistEntity
        {
            Id = ExternalPlaylistId,
            AccountId = AccountId,
            Provider = (int)ExternalProvider.Spotify,
            ExternalId = "spotify:playlist:fixture-1",
            Url = "https://open.spotify.com/playlist/fixture-1",
            Name = "Fixture Imports",
            SnapshotVersion = 1,
            LastSyncedAtUtc = now,
        };

        var canonicalTrack = new CanonicalTrackEntity
        {
            Id = TrackId,
            Artist = "Fixture Artist",
            Title = "Fixture Track",
            DurationMs = 180000,
            Isrc = "HRABC0100001",
            NormalizedArtist = "fixture artist",
            NormalizedTitle = "fixture track",
        };

        var trackSource = new TrackSourceEntity
        {
            Id = TrackSourceId,
            CanonicalTrackId = TrackId,
            Provider = (int)ExternalProvider.Spotify,
            ExternalId = "spotify:track:fixture-1",
            ExternalUrl = "https://open.spotify.com/track/fixture-1",
            RawMetadataJson = "{\"fixture\":true}",
        };

        var mediaFile = new LocalMediaFileEntity
        {
            Id = MediaFileId,
            CanonicalTrackId = TrackId,
            Path = "/music/fixtures/fixture-track.mp3",
            Size = 1024,
            LastWriteUtc = now,
            DurationMs = 180000,
            Codec = "mp3",
            Bitrate = 320,
            SampleRate = 44100,
            BitDepth = 16,
            Availability = (int)LocalMediaAvailability.Available,
        };

        var playlist = new PlaylistEntity
        {
            Id = PlaylistId,
            Name = "Fixture Playlist",
            ImportMode = (int)PlaylistImportMode.Mirror,
            ExternalPlaylistId = ExternalPlaylistId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        var playlistItem = new PlaylistItemEntity
        {
            Id = PlaylistItemId,
            PlaylistId = PlaylistId,
            Position = 1,
            ProviderItemId = "spotify:item:fixture-1",
            CanonicalTrackId = TrackId,
            Status = (int)PlaylistItemStatus.AvailableLocal,
            SnapshotJson = "{\"artist\":\"Fixture Artist\",\"title\":\"Fixture Track\"}",
        };

        var appSetting = new AppSettingEntity
        {
            Key = "fixtures:enabled",
            JsonValue = "true",
            UpdatedAtUtc = now,
        };

        var schemaInfo = new SchemaInfoEntity
        {
            Id = SchemaInfoId,
            ApplicationVersion = "dev-fixture",
            MigrationVersion = "dev-fixture",
            LastBackupUtc = null,
        };

        dbContext.AddRange(profile, account, externalPlaylist, canonicalTrack, trackSource, mediaFile, playlist, playlistItem, appSetting, schemaInfo);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new DevelopmentSeedResult(true, 10);
    }
}
