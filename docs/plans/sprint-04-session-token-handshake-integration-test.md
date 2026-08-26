# Sprint 4 session-token handshake integration test

## Goal

Add real desktop-side integration coverage proving the daemon startup handshake yields the token required for protected `/api/v1` endpoints while preserving the intended unauthenticated health surface.

## Current-state findings

- `DesktopDaemonIntegrationTests` already prove a real development daemon launch can serve authenticated versioned API requests after startup.
- `ServerHost.RequiresSessionToken()` protects `/api/v1/*` except `/api/v1/system/health`.
- Sprint 4 explicitly requires a session-token handshake test, but current desktop integration coverage does not yet assert unauthorized behavior without the handshake token or token rotation invalidation after relaunch.

## In scope

- Add a real integration test that verifies protected versioned endpoints reject requests without the session token.
- Verify `/api/v1/system/health` remains reachable without the token.
- Verify the startup handshake token unlocks protected versioned endpoints.
- Verify a relaunch rotates the session token and invalidates the old one.

## Out of scope

- Server API behavior changes.
- SignalR reconnect behavior.
- Avalonia UI automation.

## Files and projects affected

- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-session-token-handshake-integration-test.md`

## API, schema and event changes

- No API changes.
- No schema or migration changes.
- Test-only coverage expansion around the existing handshake/auth contract.

## Implementation sequence

1. Extend real daemon integration coverage with raw unauthenticated/authenticated HTTP assertions.
2. Add a relaunch test that proves the old token is rejected and the new token is accepted.
3. Run targeted desktop tests if tooling is available.

## Testing strategy

- Extend `DesktopDaemonIntegrationTests`.
- Use the real development daemon launch path through `DesktopDaemonSupervisor` and `SystemDesktopProcessLauncher`.
- If `dotnet` is unavailable, document the toolchain blocker.

## Migration and rollback

- No migration.
- Rollback is a normal test/doc revert.

## Security, privacy and license impact

- Strengthens verification of loopback-token protection already required by Sprint 4.
- No new secret persistence or logging.
- No license impact.

## Risks and stop conditions

- Stop if the current daemon intentionally leaves more `/api/v1` endpoints public than documented.
- Stop if relaunch token rotation is too flaky to be a deterministic integration test in the current environment.

## Acceptance-criteria mapping

- Directly strengthens the required session token handshake coverage.
- Reinforces secure localhost daemon behavior in the Sprint 4 desktop shell foundation.
