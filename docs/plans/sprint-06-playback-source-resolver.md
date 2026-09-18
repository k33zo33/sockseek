# Sprint 6 playback source resolver

## Goal

Add the Sprint 6 local playback source resolver foundation so the player layer can request a playable local file for a canonical track or playlist item without introducing provider audio.

## Current-state findings

- `Sockseek.Player` currently contains an empty `PlaybackCoordinator`.
- Sprint 6 local-library persistence already links `CanonicalTrackEntity` to one or more `LocalMediaFileEntity` rows and marks file availability.
- Playlist exact local matching already sets `PlaylistItemEntity.CanonicalTrackId` and `PlaylistItemStatus.AvailableLocal`.
- There is no current application-level resolver contract that the player layer can consume.

## In scope

- Add an application abstraction for resolving playback sources by canonical track id or playlist item id.
- Implement an EF-backed infrastructure resolver that returns the best available local file and a pending/unavailable result otherwise.
- Wire `PlaybackCoordinator` to that abstraction without depending on EF, Desktop, provider SDKs, or `Sockseek.Core`.
- Add automated tests for resolver behavior and player coordinator delegation.

## Out of scope

- Audio decoding or actual playback.
- Progressive Soulseek playback, buffer state, or completed download workflow playback.
- Desktop player controls consuming the resolver.
- External provider audio URLs, provider streaming, or provider download contracts.

## Files and projects affected

- `Sockseek.Application`: playback resolver contracts.
- `Sockseek.Infrastructure`: EF-backed local resolver.
- `Sockseek.Player`: coordinator uses resolver.
- `Sockseek.Infrastructure.Tests`: local resolver integration tests.
- `Sockseek.Player.Tests`: coordinator unit tests.
- `Sockseek.sln`: include new Player test project.

## API, schema and event changes

- No public daemon API change.
- No database schema change.
- No SignalR event change.
- Server composition registers the local resolver and player coordinator for future daemon use.

## Implementation sequence

1. Add application playback source models and `IPlaybackSourceResolver`.
2. Implement `LocalPlaybackSourceResolver` in Infrastructure using available `LocalMediaFiles`.
3. Update `PlaybackCoordinator` to resolve canonical track and playlist item playback sources.
4. Add Player tests with a fake resolver.
5. Add Infrastructure tests for available, missing, and unavailable playlist-item cases.
6. Register the resolver and coordinator in daemon composition without adding public playback endpoints.
7. Run targeted tests, full Release build, and full test suite.

## Testing strategy

- Player unit tests prove `PlaybackCoordinator` delegates and returns resolver results.
- Infrastructure integration tests use SQLite migrations and seeded records.
- Server composition test proves the daemon can resolve `IPlaybackSourceResolver` and `PlaybackCoordinator`.
- Full `dotnet build -c Release --no-restore` and `dotnet test -c Release --no-build`.

## Migration and rollback

- No migration is required.
- Rollback removes resolver classes/tests and restores the empty coordinator.

## Security, privacy and license impact

- Local-first only; resolver returns local filesystem paths already indexed from configured roots.
- No provider audio capability, external audio URL, `IPlaybackProvider`, `GetAudioStreamAsync`, or `DownloadTrackAsync` is introduced.
- AGPL-3.0 posture is unchanged.

## Risks and stop conditions

- Stop for ADR if playback policy expands beyond local files/completed or progressive Soulseek downloads.
- Keep Player free of EF and concrete persistence.
- Keep Desktop free of Core, EF DbContext, and provider SDKs.

## Acceptance-criteria mapping

- LocalMediaFile -> CanonicalTrack matching: player can now consume the available local media chosen for a canonical track.
- Exact local playlist match -> `AvailableLocal`: resolver can use matched playlist items to return local playback files.
- Future progressive/download playback remains out of Sprint 6 and belongs to later playback/download sprints.
