# Sprint 6 - local library

## Goal

Build the local-library vertical increment: scan configured folders, read local audio metadata, upsert `CanonicalTrack` and `LocalMediaFile` records idempotently, expose scan progress, and preserve missing track identity when files disappear.

## Current-state findings

- Domain already contains `CanonicalTrack`, `LocalMediaFile`, matching rules, and tests for identity decisions.
- Infrastructure already has `CanonicalTrackEntity`, `LocalMediaFileEntity`, `CanonicalTrackStore`, SQLite migrations, and idempotent upsert tests.
- No library root model, scanner service, metadata reader abstraction, scan progress contract, file watcher, or Desktop Library implementation exists yet.
- Existing database already has a unique index on `LocalMediaFiles.Path`.

## In scope

- Introduce library root persistence and scan state if needed for repeatable scans.
- Add an audio metadata reader abstraction plus a TagLib-backed implementation.
- Add a scanner that walks supported local audio files, reads metadata, upserts records, reports progress, and marks missing files unavailable.
- Add focused integration tests using temp directories and metadata fixtures or fakes.
- Add API/application contracts only when needed for scan progress and Library UI wiring.

## Out of scope

- External provider imports beyond exact local match plumbing.
- Player implementation and play-while-downloading.
- Content hash calculation beyond an optional placeholder or interface.
- Full 10k rendered UI profiling in the first vertical slice.

## Files and projects affected

- `Sockseek.Domain`: track/local library value behavior only if existing model needs small additions.
- `Sockseek.Application`: library scanner abstractions/use cases.
- `Sockseek.Infrastructure`: TagLib metadata reader, filesystem scan, EF persistence.
- `Sockseek.Server`: local API/progress endpoints/events if needed.
- `Sockseek.Desktop`: Library view model/UI once backend scan surface exists.
- Tests in Domain/Application/Infrastructure/Server/Desktop as touched.

## API, schema and event changes

- Expected schema addition: library roots and scan checkpoint/progress records.
- Expected API addition: library roots, scan trigger/status, local track/file list.
- Expected event addition: library scan progress snapshots or workflow-style progress updates.
- Any schema change requires EF migration and upgrade tests.

## Implementation sequence

1. Implement metadata reader abstraction and scanner core with fake reader tests.
2. Persist library roots and scan results idempotently.
3. Mark deleted/missing files unavailable during rescan without deleting canonical tracks.
4. Add scan progress model/events.
5. Add Library UI list/search and rescan/relink actions.
6. Add performance fixture for 10k rows.

## Testing strategy

- Temp-directory scan integration tests.
- Tag metadata reader fixture tests.
- Repeat scan idempotency tests.
- Delete/move/modified scan tests.
- 10k local search/list performance test.
- Desktop Library VM tests for loading, searching, unavailable rows, and action commands.

## Migration and rollback

- Use EF migration for any new tables/columns.
- Existing migration backup behavior remains mandatory before applying pending migrations.
- Rollback is database backup restore; downgrade migrations are not supported.

## Security, privacy and license impact

- Local-first only; scan reads local filesystem metadata.
- Never delete physical audio because a playlist item or scan result disappears.
- Do not log sensitive paths beyond normal local diagnostics; avoid tokens/secrets.
- No external provider audio or downloading behavior is introduced.

## Risks and stop conditions

- Stop for ADR if product scope changes audio-source policy, database choice, or process topology.
- Stop if TagLib dependency/license is incompatible with AGPL-3.0 distribution.
- Keep Desktop free of EF/Core job-object references.

## Acceptance-criteria mapping

- Repeat scan does not duplicate files: scanner upsert tests.
- Changed tags refresh: modified metadata fixture/fake-reader test.
- Deleted file becomes unavailable: rescan missing-file test.
- 10k fixture searchable: performance/list test.
- Exact local playlist match becomes `AvailableLocal`: resolver test after scanner persistence is in place.
