# Sprint 4 restart recovery integration test

## Goal

Add an end-to-end desktop integration test that proves the shell-side recovery path can survive a controlled daemon relaunch using the real desktop supervisor and SignalR recovery coordinator.

## Current-state findings

- `DesktopDaemonIntegrationTests` already prove a real dev daemon can launch and serve authenticated `/api/v1/system/*` requests.
- `DesktopBackendEventsConnectionIntegrationTests` already prove a real SignalR connection can start and subscribe.
- `DesktopBackendRecoveryCoordinatorTests` currently cover restart behavior only with fake connections.
- Sprint 4 still explicitly calls for a daemon start/restart integration test, so there is a remaining end-to-end coverage gap.

## In scope

- Add a real integration test that launches the dev daemon, starts the recovery coordinator, relaunches the daemon through the supervisor, and verifies the coordinator reconnects.
- Revalidate authenticated `/api/v1/system/health` access after relaunch using the fresh handshake.

## Out of scope

- Full Avalonia UI bootstrap.
- Window-level UX automation.
- New restart endpoints or process-control APIs.

## Files and projects affected

- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-restart-recovery-integration-test.md`

## API, schema and event changes

- No API changes.
- No schema or migration changes.
- Test-only coverage expansion.

## Implementation sequence

1. Add a real integration test around supervisor relaunch and recovery coordinator state transitions.
2. Verify authenticated system API access after relaunch.
3. Run the desktop test project.

## Testing strategy

- Run targeted `Sockseek.Desktop.Tests` inside Docker.
- Use bounded waits for asynchronous reconnect state transitions.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- Uses existing loopback-only local session handshake.
- No license impact.
- No new secret handling.

## Risks and stop conditions

- Stop if the dev daemon relaunch path is too flaky to provide a meaningful deterministic test in the current repo setup.
- Stop if relaunch requires changes to server process semantics beyond Sprint 4 scope.

## Acceptance-criteria mapping

- Directly strengthens the required daemon start/restart integration coverage.
- Supports “UI se oporavlja nakon kontroliranog restarta daemona” with real desktop-side recovery evidence.
