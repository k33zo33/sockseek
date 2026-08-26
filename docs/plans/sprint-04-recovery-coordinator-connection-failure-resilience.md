# Sprint 4 recovery coordinator connection-failure resilience

## Goal

Keep desktop backend recovery orchestration usable when the SignalR event connection cannot be established, so Sprint 4 shell state can degrade to disconnected/retryable behavior instead of faulting the coordinator task chain.

## Current-state findings

- `DesktopBackendRecoveryCoordinator` currently rethrows if `DesktopBackendEventsReconnectManager.StartAsync()` or `SubscribeAllAsync()` fails.
- A thrown transition faults `WhenIdleAsync()` for that snapshot, which makes recovery/test callers treat the coordinator itself as broken instead of seeing a clean disconnected state.
- Sprint 4 already models disconnected/restarting backend UX, so transient event-connection failures should map into that state rather than crash the recovery flow.

## In scope

- Make recovery-coordinator snapshot application resilient to event-connection startup/subscription failures.
- Preserve a disconnected events state after failure.
- Add focused tests for failure and later recovery.

## Out of scope

- New user-facing error messaging.
- Automatic exponential backoff or retry loops.
- Changes to daemon handshake semantics.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-recovery-coordinator-connection-failure-resilience.md`

## API, schema and event changes

- No API changes.
- No schema or migration changes.
- Desktop recovery behavior becomes more fault-tolerant during event-hub connection setup.

## Implementation sequence

1. Remove fatal rethrow behavior from recovery-coordinator connection setup failures.
2. Add tests for failed initial connection and successful later reconnection with a fresh handshake.
3. Run targeted desktop tests when tooling is available.

## Testing strategy

- Extend `DesktopBackendRecoveryCoordinatorTests` for connection-failure resilience and recovery.
- Run targeted desktop tests if `dotnet` is available; otherwise document the toolchain blocker.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- No new secret handling; handshake data stays unchanged.
- No license impact.
- Failure handling continues to avoid leaking session tokens.

## Risks and stop conditions

- Stop if connection failures must surface as hard startup errors for product reasons not yet documented.
- Stop if this should instead be solved inside the SignalR connection implementation with retry policy.

## Acceptance-criteria mapping

- Strengthens the backend starting/restarting/disconnected UX deliverable.
- Improves recovery robustness for controlled daemon restarts and transient event-connection failures.
