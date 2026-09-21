# Sprint 7 player now-playing metadata

## Goal

Expose local now-playing metadata through the player API and Desktop bottom player using only indexed local library/download records.

## Current-state findings

- `PlayerStateDto` currently exposes playback state, local path, volume and queue, but not title, artist, album, duration, codec or artwork metadata.
- `PlaybackSnapshot` carries `CanonicalTrackId`, `LocalMediaFileId` and local path, which can be used by the server to read local library metadata from SQLite.
- Desktop currently falls back to `Path.GetFileName` and cannot render real duration or artist metadata from the player state.
- Local cover-art persistence does not exist yet, so this slice can expose an explicit nullable artwork path without inventing storage.

## In scope

- Add an optional `PlayerNowPlayingDto` to `PlayerStateDto`.
- Populate now-playing metadata on `/api/v1/player` and all player command responses from local database records.
- Update Desktop player bar title, artist and progress formatting to prefer `NowPlaying`.
- Add focused API and Desktop tests.
- Update OpenAPI through the normal build.

## Out of scope

- New database columns or migrations.
- Embedded cover-art extraction or artwork cache files.
- Provider metadata lookup, provider artwork URLs or any external audio capability.
- Queue item metadata for every queued item.
- OS media-session publishing; that remains a separate Sprint 7 bridge task.

## Files and projects affected

- `Sockseek.Api`
- `Sockseek.Server`
- `Sockseek.Server.Tests`
- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/openapi.json`

## API, schema and event changes

- Public API contract change: `PlayerStateDto` gains nullable `NowPlaying`.
- No database schema changes.
- No SignalR event changes.

## Implementation sequence

1. Add `PlayerNowPlayingDto` to API contracts and JSON source generation.
2. Resolve now-playing metadata in server player endpoints from `SockseekDbContext`.
3. Update Desktop player bar formatting to use title, artist, album and duration from `NowPlaying`.
4. Add API/Desktop tests for metadata and fallback behavior.
5. Run Desktop/server targeted tests, full build and full test suite.

## Testing strategy

- Server integration test with a seeded local media file and player response metadata.
- Desktop view-model tests for title, artist and duration rendering from `NowPlaying`.
- Existing player state, queue, and provider-audio guard tests remain in force.

## Migration and rollback

- No migration.
- Rollback is a normal code revert plus regenerated OpenAPI revert.

## Security, privacy and license impact

- Reads only local database metadata and local paths already exposed by the local daemon.
- Does not log secrets.
- Does not add any provider playback, provider audio URL or download capability.
- No license impact.

## Risks and stop conditions

- Stop if metadata requires provider lookups or new persistent artwork storage.
- Stop if API compatibility requires a breaking DTO redesign.
- Treat missing metadata as nullable/fallback rather than failing playback.

## Acceptance-criteria mapping

- Supports Sprint 7 now-playing metadata from local files.
- Strengthens bottom player MVP without changing audio-source policy.
- Leaves cover-art extraction and OS media-session publishing as explicit remaining Sprint 7 work.
