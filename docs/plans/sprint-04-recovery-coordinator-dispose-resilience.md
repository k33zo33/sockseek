# Sprint 4 recovery coordinator dispose resilience

## Goal

Keep desktop recovery transitions usable even when tearing down an event connection throws, so reconnect/restart flows degrade to disconnected state instead of faulting the coordinator pipeline.

## Current-state findings

- `DesktopBackendRecoveryCoordinator` now handles connection creation/start/subscribe failures, but cleanup still awaits `DesktopBackendEventsReconnectManager.DisposeAsync()` directly.
- If the underlying event connection throws during disposal, restart/disconnect transitions can still fault `ApplySnapshotAsync()` and break `WhenIdleAsync()` consumers.
- Sprint 4 backend UX already has a disconnected fallback state, so cleanup failures should degrade into that state rather than abort the recovery flow.

## In scope

- Make recovery-coordinator cleanup resilient to manager disposal failures.
- Cover both active-manager teardown and failed-start cleanup paths with focused tests.

## Out of scope

- New retry policies.
- User-facing diagnostics messaging.
- Changes to event connection disposal semantics outside the coordinator.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-recovery-coordinator-dispose-resilience.md`

## API, schema and event changes

- No API changes.
- No schema or migration changes.
- Desktop recovery teardown becomes more resilient to local connection cleanup failures.

## Implementation sequence

1. Guard recovery-coordinator cleanup paths around manager disposal.
2. Add focused tests for dispose failures during restart and failed-start cleanup.
3. Run targeted desktop tests if tooling is available.

## Testing strategy

- Extend `DesktopBackendRecoveryCoordinatorTests` for disposal-failure resilience.
- Run targeted desktop tests when `dotnet` is available; otherwise document the toolchain blocker.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- No new secret handling.
- No license impact.
- Failure handling stays local to the desktop recovery layer.

## Risks and stop conditions

- Stop if event-connection disposal failures should remain fatal for debugging/visibility reasons.
- Stop if broader recovery error reporting needs a separate design pass before more resilience work.

## Acceptance-criteria mapping

- Strengthens backend restarting/disconnected UX.
- Improves robustness of controlled daemon restart handling in the desktop shell foundation.
