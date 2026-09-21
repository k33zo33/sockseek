# Sprint 7 local now-playing artwork

## Goal

Expose local now-playing artwork for the current player item by extracting embedded cover art from the local audio file into a local cache path returned by `PlayerNowPlayingDto.ArtworkPath`.

## Current-State Findings

- `PlayerNowPlayingDto` already includes nullable `ArtworkPath`.
- `ServerHost.ResolveNowPlayingAsync` currently populates title, artist, album, duration and codec from local SQLite records, but always returns `ArtworkPath: null`.
- `TagLibAudioMetadataReader` already reads local audio tags through TagLib, but `LocalAudioMetadata` has no artwork field and local media DB schema has no artwork column.
- Sprint 7 asks to read cover art and now-playing metadata from the local file.

## In Scope

- Add a local artwork extraction/cache service for the currently playing local file.
- Use only local file paths already resolved by the player; no provider artwork URLs or external lookups.
- Return a cached local image path in `PlayerNowPlayingDto.ArtworkPath` when embedded artwork exists.
- Keep missing/invalid artwork nullable and non-fatal.
- Add focused tests for extraction/cache behavior and player now-playing response wiring.

## Out of Scope

- Database schema changes for artwork.
- Provider artwork URLs or metadata lookup.
- Queue-wide artwork metadata.
- Desktop image rendering beyond receiving the path already exposed by the API.
- Artwork deduplication beyond a simple deterministic cache filename.

## Files and Projects Affected

- `Sockseek.Infrastructure`: local artwork extraction/cache helper.
- `Sockseek.Infrastructure.Tests`: fixture/cache tests.
- `Sockseek.Server`: now-playing artwork resolution.
- `Sockseek.Server.Tests`: player response coverage with embedded local artwork.
- `docs/plans`: this plan.

## API, Schema And Event Changes

- No public DTO shape change; `PlayerNowPlayingDto.ArtworkPath` is already part of the API.
- No database schema change.
- No SignalR event change.

## Implementation Sequence

1. Add a small `LocalArtworkCache` that reads the first embedded TagLib picture and writes it under a local cache directory.
2. Resolve the cache directory from `ServerOptions.ConfigDir`/database path conventions without exposing provider paths.
3. Call artwork resolution only for the current local media file path during now-playing mapping.
4. Add tests with a local audio fixture containing embedded artwork.
5. Run Infrastructure/Server targeted tests, full Release build and full test suite.

## Testing Strategy

- Infrastructure test proves embedded artwork is extracted, cached and missing artwork returns null.
- Server test proves `/api/v1/player` returns a non-null `ArtworkPath` for a seeded local file with embedded artwork.
- Existing bad-file/player tests ensure artwork failures do not fail playback.

## Migration And Rollback

- No migration.
- Rollback removes cache extraction and restores nullable `ArtworkPath` behavior.
- Cache files are derived artifacts and can be safely regenerated or ignored.

## Security, Privacy And License Impact

- Uses only local audio files and local cache files.
- Does not log paths beyond existing local player response behavior.
- Does not introduce provider audio, provider artwork URLs, provider downloads or external services.
- No new license impact if implemented with existing TagLib dependency.

## Risks And Stop Conditions

- Stop if artwork support requires a schema change without a migration.
- Stop if artwork requires provider URLs or external metadata lookup.
- Treat corrupt artwork as nullable; never fail playback due to cover extraction.

## Acceptance-Criteria Mapping

- Supports Sprint 7 local now-playing metadata and cover-art reading from local files.
- Player never attempts provider audio URL: unchanged and still covered by player tests.
