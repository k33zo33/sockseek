# Sprint 4 window daemon start busy state

## Goal

Expose bindable busy state for the window-level daemon start bridge so future desktop recovery UI can disable duplicate launch attempts and reflect in-progress startup.

## Current-state findings

- `DesktopShellWindowViewModel` now exposes `TryStartDaemonAsync` for manual daemon recovery.
- The window currently has no bindable in-progress state for that async launch operation.
- Without a busy flag, future UI would need to infer or duplicate launch lifecycle state.

## In scope

- Add `IsStartingDaemon` to the window view-model.
- Make `CanStartDaemon` respect the busy state.
- Raise property changes when manual launch starts and finishes.
- Add focused unit tests and binding-notification coverage.

## Out of scope

- Visual UI implementation.
- Error toast/reporting for failed start attempts.
- Broader generic command framework or progress infrastructure.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-window-daemon-start-busy-state.md`

## API, schema and event changes

- No API changes.
- No schema or migration changes.
- Desktop-only view-model surface addition.

## Implementation sequence

1. Add `IsStartingDaemon` and wrap `TryStartDaemonAsync`.
2. Update `CanStartDaemon` to hide the action while launching.
3. Extend unit and binding-notification tests.
4. Run desktop tests.

## Testing strategy

- Extend `DesktopShellWindowViewModelTests` for busy-state transitions.
- Extend `ShellBindingNotificationTests` for window property notifications during launch.
- Run `Sockseek.Desktop.Tests` in Docker.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- Reuses the existing secure local daemon startup path.
- No new secrets or tokens exposed.
- No license impact.

## Risks and stop conditions

- Stop if a broader async command abstraction is required first.
- Stop if busy-state semantics conflict with planned Avalonia command wiring.

## Acceptance-criteria mapping

- Strengthens backend disconnected/recovery UX.
- Makes manual daemon recovery safer by preventing duplicate launches.
