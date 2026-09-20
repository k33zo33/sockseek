# Sprint 7 - Player session lifetime

## Goal

Make playback state process-persistent so future player API endpoints do not create a fresh coordinator per request.

## Current-state findings

- `PlaybackCoordinator` holds playback state, queue state and command state in memory.
- `ServerHost` registered `PlaybackCoordinator` as scoped, which would reset state for each request scope.
- `LocalPlaybackSourceResolver` depends on EF `SockseekDbContext` and must remain scoped.

## In scope

- Register `PlaybackCoordinator` as singleton.
- Add a singleton resolver wrapper that creates a scope per source-resolution operation.
- Keep `LocalPlaybackSourceResolver` scoped for EF Core lifetime correctness.
- Add composition tests that prove coordinator state is singleton across scopes.

## Out of scope

- Player HTTP endpoints.
- Desktop player controls.
- Queue persistence hydration into the coordinator.
- Media key integration.

## Files and projects affected

- `Sockseek.Server`
- `Sockseek.Server.Tests`
- `docs/plans`

## API, schema and event changes

- No HTTP API, database schema or event changes.
- Process composition changes only.

## Implementation sequence

1. Add `ScopedPlaybackSourceResolver`.
2. Update `ServerHost` DI registrations.
3. Extend playback composition tests.
4. Run Server tests and full validation.

## Testing strategy

- Server composition test verifies singleton coordinator across scopes.
- Full Release build and test suite before commit.

## Migration and rollback

- No migration.
- Rollback restores scoped coordinator registration and direct scoped resolver registration.

## Security, privacy and license impact

- No new file paths, secrets, provider tokens or provider audio capabilities are exposed.
- Local session token behavior is unchanged.

## Risks and stop conditions

- Stop if singleton coordinator would require holding a scoped `SockseekDbContext`.
- Stop if this forces provider playback/download capabilities.

## Acceptance-criteria mapping

- Supports Sprint 7 stable player state and persistent queue work.
- Does not itself complete Desktop UI, media keys or fixture-matrix acceptance criteria.
