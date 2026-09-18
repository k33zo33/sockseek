# Sprint 7 playback queue persistence

## Goal

Persist the player queue, current index, repeat mode and deterministic shuffle seed so the queue can resume after daemon restart.

## Current-state findings

- Sprint 7 state-machine foundation exists in `Sockseek.Player`.
- `docs/DATABASE.md` already specifies `PlaybackQueue` and `PlaybackQueueItem` tables.
- `SockseekDbContext` does not yet contain playback queue entities or DbSets.
- No queue store or queue persistence tests exist.

## In scope

- Add playback queue persistence entities and EF model configuration.
- Add an EF migration for `PlaybackQueues` and `PlaybackQueueItems`.
- Add queue record/store types for save/load behavior.
- Preserve deterministic shuffle seed and repeat mode.
- Add queue persistence tests proving restart-style reload.

## Out of scope

- Desktop queue UI and media keys.
- LibVLC engine adapter and codec fixture matrix.
- Progressive Soulseek playback.
- Daemon HTTP player endpoints.

## Files and projects affected

- `Sockseek.Infrastructure`: queue entities, store and migration.
- `Sockseek.Infrastructure.Tests`: queue persistence tests.
- `docs/plans`: this plan.

## API, schema and event changes

- Adds SQLite tables through EF migration.
- No daemon HTTP API change.
- No SignalR event change.

## Implementation sequence

1. Add queue entity types and DbSets/model configuration.
2. Add queue store records and persistence operations.
3. Generate/add EF migration and model snapshot update.
4. Add SQLite integration tests for save/load/update and deterministic shuffle seed preservation.
5. Run targeted Infrastructure tests, full Release build and full test suite.

## Testing strategy

- Infrastructure SQLite migration test creates the schema from migrations.
- Queue store tests save a queue, reload it through a new context and verify current index, repeat mode, shuffle seed and item order.
- Update test verifies stale items are replaced deterministically.

## Migration and rollback

- Forward migration creates `PlaybackQueues` and `PlaybackQueueItems`.
- Down migration drops queue tables.
- Rollback restores a backed-up database; downgrade is otherwise unsupported by product policy.

## Security, privacy and license impact

- Queue state is local-only SQLite data.
- No provider secrets, provider audio URLs or external playback contracts are stored.

## Risks and stop conditions

- Stop if queue persistence requires storing provider audio URLs or external streaming identifiers.
- Stop for ADR if queue schema diverges from the documented SQLite decision.

## Acceptance-criteria mapping

- Queue is restored after restart: covered by queue save/reload test.
- Deterministic shuffle: seed is persisted with queue state for later shuffle ordering logic.
