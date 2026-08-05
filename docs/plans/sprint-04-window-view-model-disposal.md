# Sprint 4 window view-model disposal

## Goal

Make the desktop shell window view-model detach its event subscriptions when disposed so the shell chrome foundation does not keep stale top-level listeners alive.

## Current-state findings

- `DesktopShellWindowViewModel` subscribes to `Session.Shell.PropertyChanged`.
- It also subscribes to `Session.Shell.CommandPalette.PropertyChanged`.
- The window view-model currently has no disposal path to detach those subscriptions.

## In scope

- Add deterministic subscription cleanup for `DesktopShellWindowViewModel`.
- Extend focused tests to verify disposal stops further notifications.

## Out of scope

- Session disposal changes.
- Avalonia `Window` lifecycle integration.
- Broader ownership refactors for shell state.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-window-view-model-disposal.md`

## API, schema and event changes

- No API changes.
- No persistence changes.
- Desktop shell chrome gains a safer lifecycle boundary.

## Implementation sequence

1. Add disposal/unsubscribe behavior to the window view-model.
2. Extend focused unit and notification tests.
3. Run desktop tests.

## Testing strategy

- Extend `DesktopShellWindowViewModelTests` to verify disposal is idempotent and detaches listeners.
- Extend `ShellBindingNotificationTests` so post-disposal shell changes do not flow into the window model.
- Run `Sockseek.Desktop.Tests` in Docker.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- No secret handling changes.
- No license impact.
- Reduces stale shell subscriptions and future lifecycle leaks.

## Risks and stop conditions

- Stop if the window view-model is intentionally immortal for the whole process lifetime.
- Stop if disposal semantics conflict with planned Avalonia ownership.

## Acceptance-criteria mapping

- Strengthens the desktop shell foundation.
- Improves lifecycle safety for the top-level window composition layer.
