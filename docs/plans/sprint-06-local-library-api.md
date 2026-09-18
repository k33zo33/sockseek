# Sprint 6 local library API

## Goal

Expose the Sprint 6 local library persistence and scanner work through the versioned local daemon API and generated desktop API client, without changing provider scope or introducing external audio playback.

## Current-state findings

Sprint 6 already has infrastructure for library roots, local scans, TagLib metadata reads, local search, duplicate grouping and manual relink. The desktop-facing API did not yet expose those capabilities, so the UI could not manage roots or inspect indexed local tracks through the daemon boundary.

During endpoint integration testing, scanning multiple local files for one canonical track exposed a same-context EF concurrency conflict in `CanonicalTrackStore` when adding additional local media files.

## In scope

- Versioned `/api/v1/library/*` endpoints for roots, scan, track search, duplicate groups and relink.
- API DTOs, source-generation metadata and `SockseekApiClient` methods for those endpoints.
- Server-side adapter from API DTOs to existing infrastructure services.
- OpenAPI regeneration and contract tests for the new routes.
- Integration tests covering auth, root management, scan, search, duplicate groups, relink and root deletion.
- Persistence fix and tests for importing multiple local files for one canonical track.

## Out of scope

- Desktop Library screen implementation.
- Background file watcher service.
- Content hashing jobs.
- Playback source resolver implementation.
- New provider integrations or provider audio access.

## Files and projects affected

- `Sockseek.Api`: DTOs, protocol enum, JSON context and HTTP client methods.
- `Sockseek.Server`: endpoint mapping, server options and local library endpoint service.
- `Sockseek.Infrastructure`: concurrency/store behavior needed by duplicate local file scans.
- `Sockseek.Infrastructure.Tests` and `Sockseek.Server.Tests`: scanner/store/API coverage.
- `docs/openapi.json`: regenerated daemon contract.

## API, schema and event changes

Adds versioned local-only endpoints:

- `GET /api/v1/library/roots`
- `POST /api/v1/library/roots`
- `DELETE /api/v1/library/roots/{rootId}`
- `POST /api/v1/library/scan`
- `GET /api/v1/library/tracks`
- `GET /api/v1/library/duplicates`
- `POST /api/v1/library/files/{localMediaFileId}/relink`

No database schema change is introduced in this API slice. Existing Sprint 6 migrations remain responsible for library roots and local library indexes. No SignalR event contract is added in this slice.

## Implementation Sequence

1. Add request/response DTOs and JSON source-generation entries.
2. Add API client methods for root management, scan, search, duplicates and relink.
3. Add server endpoint service that resolves the local SQLite path, runs migrations lazily and maps infrastructure records to DTOs.
4. Map protected `/api/v1/library/*` endpoints in `ServerHost`.
5. Regenerate OpenAPI and extend contract tests.
6. Add server integration coverage using a temporary SQLite database and WAV fixtures.
7. Fix and cover same-track duplicate local file scan/import behavior.

## Testing Strategy

- Targeted server integration tests for endpoint auth and end-to-end root/scan/search/duplicate/relink/delete behavior.
- Infrastructure tests for canonical track duplicate local files and scanner duplicate import.
- Existing concurrency-token test to preserve multi-context conflict detection.
- Full Release build and full test suite before commit.

## Migration and Rollback

No new migration is required. Rollback removes the API surface and server adapter while leaving prior Sprint 6 persistence migrations intact. The concurrency fix is backward-compatible and only prevents false same-context conflicts.

## Security, Privacy and License Impact

Endpoints remain under the existing `/api/v1` local session token middleware except public health. Library root deletion removes only configuration records and never physical audio files. The API exposes local file paths to authenticated local clients only. No provider token, provider audio URL, playback provider or external audio download capability is introduced. AGPL-3.0 posture is unchanged.

## Risks and Stop Conditions

- Stop if API work requires changing process topology, provider scope, playback policy or database technology; that would need an ADR.
- Watcher and UI work should not be hidden inside this API slice.
- Long-running scans may need background job/event progress in a later Sprint 6 slice.

## Acceptance-Criteria Mapping

- Repeated scan no duplicates: covered by existing scanner tests.
- Changed tags refresh: covered by existing scanner tests.
- Deleted file unavailable without deleting track identity: covered by existing scanner tests.
- 10,000 track search/display without freezing: query performance test exists for the search backend; UI display remains future work.
- Playlist exact local match becomes `AvailableLocal`: covered by existing resolver tests; endpoint consumption remains future UI/application work.
