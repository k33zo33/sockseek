# Sprint 4 window command-palette bridge

## Goal

Expose a small window-level bridge for command-palette interactions so the future Avalonia shell can drive top-level keyboard navigation through `DesktopShellWindowViewModel` instead of reaching through nested shell state.

## Current-state findings

- `DesktopShellWindowViewModel` already bridges diagnostics and daemon-start actions.
- Command-palette state and actions still require callers to navigate through `Session.Shell` directly.
- Sprint 4’s shell foundation benefits from a top-level bridge for keyboard-driven shell interactions.

## In scope

- Expose window-level command-palette state.
- Add pass-through methods for opening, closing, shortcut handling, and item execution.
- Forward property-change notifications for palette open/close state.
- Extend focused unit and notification tests.

## Out of scope

- Actual Avalonia keybinding markup.
- Command-palette filtering or search text.
- Additional command types beyond the current shell palette items.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-window-command-palette-bridge.md`

## API, schema and event changes

- No API changes.
- No persistence changes.
- Desktop window composition gains a cleaner shell interaction surface.

## Implementation sequence

1. Add window-level palette state and pass-through methods.
2. Forward palette property-change notifications.
3. Extend focused tests.
4. Run desktop tests.

## Testing strategy

- Extend `DesktopShellWindowViewModelTests` for palette bridge behavior.
- Extend `ShellBindingNotificationTests` for window-level palette-state notifications.
- Run `Sockseek.Desktop.Tests` in Docker.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- No secret handling changes.
- No license impact.
- Improves top-level shell wiring for the future Avalonia desktop UI.

## Risks and stop conditions

- Stop if command-palette behavior needs a broader input/focus model first.
- Stop if the window layer should deliberately avoid shell interaction forwarding.

## Acceptance-criteria mapping

- Strengthens the desktop shell and command-palette foundation.
- Improves keyboard-driven navigation ergonomics for the main shell.
