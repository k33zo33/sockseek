# Sprint 7 playback queue persistence

## Goal

Persist the player queue, current index, repeat mode and deterministic shuffle seed so the queue can resume after daemon restart.

## Current-state findings

- Sprint 7 state-machine foundation exists in `Sockseek.Player`.
- `docs/DATABASE.md` already specifies `PlaybackQueue` and `PlaybackQueueItem` tables.
- `SockseekDbContext` now contains playback queue entities, DbSets and migration metadata.
- The first implementation persisted queue seed but not the explicit shuffle enabled flag; restore must preserve both.
- Server startup restores the default persisted queue when queue tables already exist, while queue save paths run the shared server migration service before writing.

## In scope

- Add playback queue persistence entities and EF model configuration.
- Add an EF migration for `PlaybackQueues` and `PlaybackQueueItems`.
- Add queue record/store types for save/load behavior.
- Preserve deterministic shuffle seed and repeat mode.
- Add queue persistence tests proving restart-style reload.
- Wire the daemon's process-scoped player coordinator to a local default persisted queue.
- Share server-side SQLite migration execution between library endpoints and queue save paths.

## Out of scope

- Desktop queue UI and media keys.
- LibVLC engine adapter and codec fixture matrix.
- Progressive Soulseek playback.
- New daemon HTTP queue mutation endpoints.

## Files and projects affected

- `Sockseek.Infrastructure`: queue entities, store and migration.
- `Sockseek.Infrastructure.Tests`: queue persistence tests.
- `Sockseek.Server`: default queue restore/save bridge and shared migration service.
- `Sockseek.Server.Tests`: restart-style queue restore endpoint coverage.
- `docs/plans`: this plan.

## API, schema and event changes

- Adds SQLite tables through EF migration.
- Adds `ShuffleEnabled` to the persisted playback queue state.
- No daemon HTTP API change.
- No SignalR event change.

## Implementation sequence

1. Add queue entity types and DbSets/model configuration.
2. Add queue store records and persistence operations.
3. Generate/add EF migration and model snapshot update.
4. Add SQLite integration tests for save/load/update and deterministic shuffle flag/seed preservation.
5. Add server restore coverage for loading the default queue on daemon startup.
6. Run targeted Infrastructure and Server tests, full Release build and full test suite.

## Testing strategy

- Infrastructure SQLite migration test creates the schema from migrations.
- Queue store tests save a queue, reload it through a new context and verify current index, repeat mode, shuffle seed and item order.
- Update test verifies stale items are replaced deterministically.
- Server endpoint test seeds the default queue in local SQLite, starts the daemon and verifies `/api/v1/player` exposes the restored queue.

## Migration and rollback

- Forward migration creates `PlaybackQueues` and `PlaybackQueueItems`.
- Queue write paths run the shared SQLite migration runner before saving so a fresh local profile can persist player queue state.
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
- Queue is restored after daemon restart: covered by server startup restore test.
- Deterministic shuffle: enabled flag and seed are persisted with queue state for later shuffle ordering logic.
