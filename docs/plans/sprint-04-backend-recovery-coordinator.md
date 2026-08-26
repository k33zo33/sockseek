# Sprint 4 backend recovery coordinator

## Goal

Add the smallest desktop-side coordinator that reacts to daemon supervisor state changes and automatically recreates the SignalR event session after a controlled backend restart.

## Current-state findings

- `DesktopDaemonSupervisor` already publishes snapshot changes for starting, connected, restarting, disconnected, and unauthorized states.
- `DesktopBackendEventsReconnectManager` already manages a single SignalR connection lifecycle and subscriptions.
- There is currently no glue that watches supervisor state and replaces the reconnect manager when a new daemon handshake arrives.
- This leaves a gap in Sprint 4's recovery requirement even though the lower-level pieces already exist.

## In scope

- Add a desktop recovery coordinator that listens to supervisor snapshots.
- Create and start a reconnect manager when a connected handshake is available.
- Dispose the active reconnect manager when the backend restarts, disconnects, becomes unauthorized, or returns to starting.
- Re-establish `SubscribeAll` on the new connection after reconnecting to a restarted backend.
- Add focused unit tests for restart recovery behavior.

## Out of scope

- Full Avalonia bootstrap or window lifecycle wiring.
- Snapshot refetch logic for future feature pages.
- User-facing restart controls.
- Broader state persistence beyond existing shell and connection state models.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-backend-recovery-coordinator.md`

## API, schema and event changes

- No server API changes.
- No schema or migration changes.
- Desktop-only lifecycle coordination changes.

## Implementation sequence

1. Add a recovery coordinator that serializes supervisor-driven transitions.
2. Reuse the existing reconnect manager and connection interface instead of introducing new transport concepts.
3. Add unit tests for initial connect, restart disposal, and reconnect with a fresh handshake.
4. Run targeted desktop tests.

## Testing strategy

- Add focused unit tests in `Sockseek.Desktop.Tests` with fake event hub connections.
- Run the desktop test project in Docker.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- No secret handling changes.
- Uses existing loopback-only handshake and bearer token flow.
- No license impact.

## Risks and stop conditions

- Stop if automatic recovery requires new server restart endpoints or altered handshake semantics.
- Stop if the coordinator needs future-sprint page snapshot logic to stay correct.

## Acceptance-criteria mapping

- Directly contributes to “UI se oporavlja nakon kontroliranog restarta daemona”.
- Builds on the existing daemon supervisor and SignalR reconnect manager without introducing future sprint scope.
