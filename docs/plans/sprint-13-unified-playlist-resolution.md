# Sprint 13 Unified Playlist Resolution

## Goal

Connect imported playlists to local-library matching, Soulseek search/download workflows and player actions so a provider-imported playlist can move toward a fully local playable state without contacting provider audio services.

## Current-state findings

- Provider imports persist `ExternalPlaylist`, `Playlist` and `PlaylistItem` rows through `ExternalPlaylistSnapshotStore`.
- `PlaylistLocalMatchResolver` can resolve imported items against available local media using source mapping, ISRC, MusicBrainz Recording MBID, normalized artist/title and duration.
- `LocalPlaybackSourceResolver` can play only playlist items already marked `AvailableLocal`; unresolved items return `PendingResolution`.
- `ISoulseekEngineGateway` can start track search jobs and candidate downloads, and can expose workflow-scoped events.
- `ServerHost` exposes provider import, player, job and workflow APIs, but does not yet expose playlist detail, playlist item resolution summaries, or bulk playlist operations.
- `DownloadWorkflowEntity` can associate Core workflow state with a `PlaylistItemId`, but current search/download gateway paths do not consistently persist playlist item workflow recovery records for imported playlist resolution.
- Desktop has provider/account cards and player surfaces, but no full playlist-detail workflow with Resolve, Download missing, Play available or review queue controls.

## In scope

- Add playlist detail/query DTOs and API endpoints for local playlists and items.
- Add playlist resolution summary counts: available, unresolved/missing, review, downloading, failed, skipped and removed.
- Add a backend playlist resolution service that first runs local matching and then prepares unresolved items for Soulseek search/download.
- Start bulk resolve/download operations through existing `ISoulseekEngineGateway` and persist workflow links to playlist items.
- Preserve manual/review outcomes and avoid overwriting successful local mappings during provider sync.
- Add backend tests for imported playlist fixtures, local matching, bulk download submission, partial success and workflow recovery state.
- Add initial Desktop view-model/API-client support for playlist detail and bulk actions after backend contracts are stable.

## Out of scope

- Provider audio playback, provider audio URLs, provider downloads or external streaming.
- Write-back to Spotify, YouTube, Bandcamp or any provider playlist.
- New provider implementations.
- Broad redesign of the player engine or Soulseek engine.
- Public packaging, legal release notes and compliance hardening beyond provider-audio guard scans.

## Files and projects affected

- `Sockseek.Api`: playlist detail, bulk operation request/response DTOs and JSON context/client methods.
- `Sockseek.Application`: resolution orchestration contracts and workflow models if a reusable application boundary is needed.
- `Sockseek.Infrastructure`: playlist query/projection store, resolution persistence and workflow link persistence.
- `Sockseek.Server`: `/api/v1/playlists` endpoints and orchestration wiring.
- `Sockseek.Desktop`: playlist detail view-model and API client usage after backend is covered.
- Tests in `Sockseek.Infrastructure.Tests`, `Sockseek.Server.Tests`, `Sockseek.Api`/client tests where applicable, and `Sockseek.Desktop.Tests`.
- `docs/openapi.json` when API contracts change.

## API, schema and event changes

- Expected API additions:
  - `GET /api/v1/playlists`
  - `GET /api/v1/playlists/{playlistId}`
  - `POST /api/v1/playlists/{playlistId}/resolve-local`
  - `POST /api/v1/playlists/{playlistId}/download-missing`
  - item-level retry/skip/review endpoints if needed by the backend slice.
- Expected DTOs include playlist summary/detail, item status, resolution summary and bulk operation result.
- No schema change is expected for the first vertical slice if existing `PlaylistItems`, `ResolutionAttempts` and `DownloadWorkflows.PlaylistItemId` are enough.
- If durable manual review decisions require additional columns/tables, add EF migration and upgrade tests in the same chunk.

## Implementation Sequence

1. Add this Sprint 13 execution plan and mark Sprint 13 in progress.
2. Add playlist query projection records/store for list/detail views with summary counts.
3. Expose read-only playlist list/detail API endpoints and update OpenAPI/client contracts.
4. Add a local resolution endpoint that runs `PlaylistLocalMatchResolver` and returns updated summary counts.
5. Add bulk download-missing orchestration for unresolved items using existing track search/download pathways, preserving playlist item IDs in workflow persistence.
6. Add retry/skip/review persistence where needed for partial success and user decisions.
7. Add Desktop playlist detail view-model states and API calls for Resolve, Download missing and Play available.
8. Run full validation, provider-audio scans and update Sprint 13 status only after acceptance criteria pass.

## Testing Strategy

- Fixture imported playlists from Spotify, YouTube and Bandcamp snapshots.
- Local matching tests for source mapping, ISRC, MBID and fuzzy review thresholds.
- Server API tests for playlist list/detail and local resolve.
- Bulk download tests proving unresolved items start Soulseek workflows and successful items are not restarted.
- Restart/recovery tests proving persisted playlist item workflow links survive daemon restart or are marked retryable.
- Manual review persistence tests that survive provider sync.
- Desktop view-model tests for summary counts, filters and bulk command enablement.
- Full validation: `dotnet restore`, `dotnet build -c Release`, `dotnet test -c Release --no-build`, `git diff --check`, provider-audio forbidden symbol scan.

## Migration and Rollback

- Prefer no migration for the first read/local-resolve slice.
- If workflow recovery or review decisions need schema changes, add EF migration plus upgrade tests before merging that chunk.
- Rollback of API/view-model changes should leave imported playlists, local media files and download records intact.

## Security, Privacy and License Impact

- Bulk playlist resolution may submit Soulseek searches/downloads, but never contacts provider audio services.
- Provider tokens and credentials remain hidden in `ISecretStore`; playlist detail DTOs expose only local/import metadata.
- No physical audio file is deleted when an external playlist item is removed or skipped.
- AGPL-3.0 posture remains unchanged.

## Risks and Stop Conditions

- Stop and request an ADR if implementing acceptance criteria requires provider audio URLs, provider playback, provider downloads or provider write-back.
- Stop and plan a migration if durable review decisions cannot fit existing persistence safely.
- Treat lost workflow recovery, destructive file deletion or provider-token exposure as blockers.
- Keep implementation vertical and test-backed; avoid building the full Desktop UX before backend contracts are proven.

## Acceptance-Criteria Mapping

- Imported playlist can become local/playable: backend list/detail, local resolve, bulk download and Play available slices.
- Restart during bulk download preserves state: workflow link persistence and recovery tests.
- User review survives provider sync: resolution attempt/review persistence tests.
- Play never contacts provider audio: player source resolver remains local/progressive-only and provider-audio scans stay clean.
- Partial success is clear/continuable: summary DTOs, failed/skipped/retry states and desktop command tests.
