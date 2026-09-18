# Sprint 6 library album browse

## Goal

Complete the Sprint 6 local-library browse surface so Desktop can show useful artist, album, and track views from local files through the daemon API.

## Current-state findings

- Local scan/search already persists canonical track identity, local file availability, codec properties, duplicate groups, relink, file watcher updates, and optional content hashes.
- Desktop now has a Library screen for roots, scans, local track search, duplicate groups, and bounded track display.
- `LocalAudioMetadata` and imported playlist snapshots include album-like metadata, but `CanonicalTrackEntity`, `LocalMediaFileEntity`, `LocalLibraryTrackRecord`, and `LocalLibraryTrackDto` do not expose album for indexed local files.
- `docs/DATABASE.md` already lists `Album` as intended persistence, but the current EF model has no album table or local-library album column.

## In scope

- Persist album title for local-library scan results with the smallest backward-compatible schema change.
- Carry album through infrastructure query records, API DTOs, server mappings, generated OpenAPI, and Desktop Library view models.
- Add Desktop grouping or filterable summaries that expose artist, album, and track views without loading more than the bounded search result page.
- Add migration/upgrade coverage and focused API/Desktop tests.

## Out of scope

- Provider album sync semantics beyond already imported metadata.
- MusicBrainz release/release-group normalization.
- Full album art, play queue integration, and playback source resolution.
- A normalized multi-table artist/album model unless the small album-title addition proves insufficient.

## Files and projects affected

- `Sockseek.Infrastructure`: metadata record, EF entity/migration, scanner/store/query mapping.
- `Sockseek.Api`: local library DTOs, JSON source generation.
- `Sockseek.Server`: local library endpoint mapping and OpenAPI output.
- `Sockseek.Desktop`: Library view model and AXAML browse grouping.
- Tests in `Sockseek.Infrastructure.Tests`, `Sockseek.Server.Tests`, and `Sockseek.Desktop.Tests`.

## API, schema and event changes

- Add nullable album/title metadata to local-library track responses.
- Add an EF migration for the selected persistence shape.
- Regenerate `docs/openapi.json`.
- No new SignalR event is planned for this slice.

## Implementation sequence

1. Confirm whether a nullable `AlbumTitle` on `CanonicalTracks` is sufficient for Sprint 6 browse, or whether local file records need their own album field.
2. Add EF migration and update model snapshot.
3. Extend metadata scan/upsert and local query records.
4. Extend API DTOs/client/server mappings and OpenAPI tests.
5. Extend Desktop Library VM with album-aware track rows and artist/album summary groups.
6. Add tests for scan persistence, search DTO album propagation, bounded Desktop album grouping, and schema upgrade.
7. Run targeted tests, full Release build, and full test suite.

## Testing strategy

- Scanner test proves album metadata is persisted and refreshed on tag change.
- Query/API tests prove album returns with local library search.
- Desktop VM test proves artist/album/track grouping works without loading unbounded rows.
- Existing migration-count/upgrade tests updated for the new migration.
- Full `dotnet build -c Release --no-restore` and `dotnet test -c Release --no-build`.

## Migration and rollback

- Migration is additive and nullable.
- Rollback is restore from database backup; downgrade migrations remain unsupported.
- Existing scanned files without album metadata remain valid and display as unknown/blank album.

## Security, privacy and license impact

- Remains local-first and reads only local metadata from user-selected library roots.
- No provider audio, external audio URLs, playback provider contract, or external download behavior is introduced.
- Local file paths and metadata remain exposed only to authenticated local clients.
- AGPL-3.0 posture is unchanged.

## Risks and stop conditions

- Stop for ADR if this grows into a normalized cross-provider artist/album identity redesign.
- Stop if adding album metadata requires changing locked provider-audio or playback-source decisions.
- Keep Desktop free of EF/Core job-object references.

## Acceptance-criteria mapping

- Artist/album/track views: this slice completes album visibility on top of existing artist/track search.
- 10,000 track fixture searchable/displayable: keep query/page limits and add grouped Desktop summaries over bounded results.
- Repeated scan, changed tags, deleted file unavailable, and exact local playlist match remain covered by existing Sprint 6 tests.
