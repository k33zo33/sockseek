# Sprint 4 bindable shell notifications

## Goal

Make the desktop shell state bindable by adding property-change notifications to the existing shell and window view-models.

## Current-state findings

- `ShellNavigationViewModel` and `DesktopShellWindowViewModel` currently expose mutable state but do not notify observers when that state changes.
- `CommandPaletteViewModel` updates `IsOpen` internally without change notifications.
- This limits the current Sprint 4 shell to pull-based tests instead of realistic desktop bindings.

## In scope

- Add a lightweight observable base class.
- Add `INotifyPropertyChanged` support to shell navigation, command palette, and shell window view-models.
- Add focused tests for navigation/theme/backend/window-title notifications.

## Out of scope

- Avalonia XAML or actual binding markup.
- Full reactive/event-stream infrastructure.
- Property notifications for immutable leaf models that do not mutate.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-bindable-shell-notifications.md`

## API, schema and event changes

- No API changes.
- No schema or migration changes.
- Desktop-only view-model behavior change.

## Implementation sequence

1. Add observable base helper.
2. Wire property change notifications into mutable shell models.
3. Add tests for shell/window notifications.
4. Run desktop tests.

## Testing strategy

- Add unit tests that capture `PropertyChanged` notifications.
- Run `Sockseek.Desktop.Tests` in Docker.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- No secrets, auth, or persistence changes.
- No license impact.
- Pure desktop binding scaffolding.

## Risks and stop conditions

- Stop if useful notification behavior requires Avalonia-specific infrastructure first.
- Stop if notification wiring forces a broader view-model rewrite.

## Acceptance-criteria mapping

- Strengthens the `Avalonia app shell, sidebar, routing` deliverable.
- Makes existing Sprint 4 shell state practical for future real UI binding.
