# Sprint 6 background library watcher

## Goal

Wire the existing local library file watcher into the daemon so configured roots are scanned in the background and changed audio files are picked up without requiring a manual scan.

## Current-state findings

- `LocalLibraryFileWatcher` detects supported audio file create/change/delete/rename events and has focused tests.
- `LocalLibraryEndpointService` can list roots and scan enabled roots, including migration setup and scan checkpoints.
- `ServerHost` does not currently start a background watcher or background scan loop for library roots.

## In scope

- Add a daemon hosted service that watches enabled local library roots.
- Debounce file events before triggering a local library scan.
- Refresh watched root configuration periodically so newly saved roots are picked up.
- Add a server integration test proving background scan and watcher-triggered rescan.

## Out of scope

- Public API or SignalR scan-progress streaming.
- Desktop live scan progress UI.
- Physical file deletion or cleanup.
- Future playback controls or audio decoding.

## Files and projects affected

- `Sockseek.Server`: hosted service and service registration.
- `Sockseek.Server.Tests`: background watcher integration test.
- `docs/plans`: this plan.

## API, schema and event changes

- No public daemon API change.
- No database schema change.
- No SignalR event change.
- Daemon startup behavior changes by adding a local background watcher service.

## Implementation sequence

1. Add `LocalLibraryBackgroundScanHostedService`.
2. Register it in `ServerHost`.
3. Use `LocalLibraryEndpointService` as the single scan/migration path.
4. Add a test with shortened refresh/debounce intervals.
5. Run targeted server tests, full Release build, and full test suite.

## Testing strategy

- Server integration test creates a configured library root, starts the hosted service, waits for background indexing, writes a second supported audio file, and verifies watcher-triggered indexing.
- Existing Infrastructure watcher/scanner tests remain the lower-level coverage.

## Migration and rollback

- No migration required.
- Rollback removes the hosted service registration and class.

## Security, privacy and license impact

- Local-first only; the service watches only configured local roots.
- No external provider audio, provider URLs, or provider download contracts are introduced.
- The scan path never deletes physical audio files.

## Risks and stop conditions

- Stop for ADR if background behavior requires changing process topology or audio-source policy.
- Keep Desktop free of EF/Core references.
- Keep provider integrations out of playback and local scan paths.

## Acceptance-criteria mapping

- Background scan and file watcher: daemon now starts a watched/debounced scan loop for configured roots.
- Deleted/moved/modified detection remains covered by existing watcher and scanner tests; this slice wires that behavior into daemon runtime.
