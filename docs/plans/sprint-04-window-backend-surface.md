# Sprint 4 window backend surface

## Goal

Expose backend state and handshake presence as first-class window-level shell properties so future Avalonia window composition can bind top-level backend UX without reaching through nested shell state.

## Current-state findings

- `DesktopShellWindowViewModel` already exposes top-level shell surfaces like page, banner, player, and command palette.
- Backend state currently requires callers to read through `Shell.BackendState` or `Shell.CurrentHandshake`.
- A consistent window-level surface makes future shell bindings simpler and more uniform.

## In scope

- Add top-level backend state and current-handshake properties to the window view-model.
- Forward shell notifications for those properties.
- Extend focused tests.

## Out of scope

- New daemon behavior.
- API or SignalR changes.
- Avalonia view markup.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-window-backend-surface.md`

## API, schema and event changes

- No API changes.
- No persistence changes.
- Desktop shell composition becomes more uniform at the window layer.

## Implementation sequence

1. Expose backend state and handshake on the window view-model.
2. Forward property-change notifications.
3. Extend focused tests.
4. Run desktop tests.

## Testing strategy

- Extend `DesktopShellWindowViewModelTests` for top-level backend surface exposure.
- Extend `ShellBindingNotificationTests` for backend-surface notifications.
- Run `Sockseek.Desktop.Tests` in Docker.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- No secret handling changes.
- No license impact.
- Improves shell composition ergonomics without changing secret exposure rules.

## Risks and stop conditions

- Stop if the window layer should intentionally keep backend state nested.
- Stop if future composition plans use a separate aggregated shell snapshot instead.

## Acceptance-criteria mapping

- Strengthens the desktop shell composition deliverable.
- Supports clearer top-level binding for backend state UX.
