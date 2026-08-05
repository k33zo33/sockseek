# Sprint 4 backend client foundation

## Goal

Connect the desktop daemon handshake to a concrete backend client factory and add a real launch-to-API integration test for the development daemon path.

## Current-state findings

- `Sockseek.Desktop` already has handshake parsing, daemon supervision, a development launch-request factory, and a concrete child-process launcher.
- `Sockseek.Server` now emits an explicit opt-in startup handshake for desktop-supervised launches.
- The desktop project still lacks a small reusable factory that turns a validated handshake into authenticated daemon HTTP client objects and a canonical events URI.
- Existing desktop tests cover fake launch flows but do not yet prove a real child daemon can be launched and queried end-to-end.

## In scope

- Add a desktop backend client factory built from `DesktopDaemonHandshake`.
- Create authenticated `HttpClient`/`SockseekApiClient` instances from the handshake.
- Expose the canonical SignalR events URI derived from the handshake base URL.
- Add focused unit tests for the factory.
- Add a real daemon launch integration test that verifies handshake + authenticated versioned API access.

## Out of scope

- Full SignalR event subscription logic.
- Full reconnect manager behavior.
- Avalonia app bootstrap and UI composition.
- Packaged daemon discovery.
- Theme persistence and command palette work.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-backend-client-foundation.md`

## API, schema and event changes

- No server API contract changes.
- No database or migration changes.
- Desktop-side only: handshake-derived client construction.

## Implementation sequence

1. Add a small desktop backend client factory that validates inputs and derives authenticated clients from the handshake.
2. Add an event hub URI helper rooted at `/api/events`.
3. Add focused unit tests for base address normalization and bearer token application.
4. Add an integration test that launches the real development daemon, waits for handshake, then calls `/api/v1/system/info` and `/api/v1/system/health` through the desktop-side client factory.
5. Run targeted desktop tests in Docker.

## Testing strategy

- Targeted `Sockseek.Desktop.Tests` run in Docker.
- Unit tests for the client factory.
- Integration test for real daemon launch and authenticated API access.

## Migration and rollback

- No migration.
- Rollback is a normal code revert with no persisted state changes.

## Security, privacy and license impact

- Reuses the existing local session token flow without widening exposure.
- Keeps client communication localhost-scoped and authenticated.
- No license impact.

## Risks and stop conditions

- Stop if the integration test requires hard-coded machine-specific paths.
- Stop if the real daemon launch path cannot be kept deterministic in CI/container test runs.
- Stop if client construction would require a direct dependency on `Sockseek.Server` internals.

## Acceptance-criteria mapping

- Contributes to “user does not have to manually start daemon”.
- Strengthens “secure session handshake”.
- Moves toward the sprint deliverable of a development shell connected to a real local daemon.
