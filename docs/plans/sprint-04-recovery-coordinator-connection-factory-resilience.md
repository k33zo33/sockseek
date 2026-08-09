# Sprint 4 recovery coordinator connection-factory resilience

## Goal

Keep desktop recovery orchestration usable when event-connection creation itself fails, so a bad handshake-derived connection setup degrades to disconnected state instead of faulting the coordinator transition chain.

## Current-state findings

- `DesktopBackendRecoveryCoordinator` now swallows `StartAsync()` and `SubscribeAllAsync()` failures, but it still creates the event connection outside that failure-handling block.
- If `connectionFactory(snapshot.Handshake)` throws, `ApplySnapshotAsync()` faults and later snapshots risk being blocked behind a faulted transition task.
- Sprint 4 already models disconnected backend UX, so connection-construction failures should map into that state just like connection-start failures.

## In scope

- Extend recovery-coordinator failure handling to include event-connection construction.
- Add focused tests for factory failure and later recovery with a fresh handshake.

## Out of scope

- New retry policies.
- User-facing error messaging.
- Changes to daemon handshake format or validation.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-recovery-coordinator-connection-factory-resilience.md`

## API, schema and event changes

- No API changes.
- No schema or migration changes.
- Desktop recovery startup becomes more resilient to local connection setup failures.

## Implementation sequence

1. Move connection creation under the existing resilience path.
2. Add focused tests for factory failure and subsequent successful reconnect.
3. Run targeted desktop tests if tooling is available.

## Testing strategy

- Extend `DesktopBackendRecoveryCoordinatorTests` for connection-factory failure behavior.
- Run targeted desktop tests when `dotnet` is available; otherwise document the toolchain blocker.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- No new secret handling; handshake/session token redaction behavior is unchanged.
- No license impact.
- Keeps failure handling local to desktop runtime abstractions.

## Risks and stop conditions

- Stop if connection-construction failures should instead be treated as fatal configuration errors.
- Stop if broader transition-chain fault handling needs a dedicated design pass first.

## Acceptance-criteria mapping

- Strengthens backend disconnected/restarting UX.
- Improves resilience of controlled daemon recovery when local event-connection setup fails.
