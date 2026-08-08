# Sprint 4 window navigation surface

## Goal

Expose sidebar navigation state directly on `DesktopShellWindowViewModel` so the future Avalonia shell can bind the top-level window to section selection without reaching through nested shell objects for common navigation data.

## Current-state findings

- `DesktopShellWindowViewModel` currently exposes the nested `Shell` object plus `CurrentPage`, but not a window-level `CurrentSection` or navigation item list.
- `ShellNavigationViewModel` already owns the authoritative sidebar item collection and selected section state.
- Future window bindings would otherwise mix direct window properties with nested `Shell.*` bindings for common chrome state.

## In scope

- Add window-level `NavigationItems` and `CurrentSection` surfaces.
- Forward property change notifications when the selected section changes.
- Extend focused tests and binding-notification coverage.

## Out of scope

- Avalonia markup or actual sidebar rendering.
- New navigation commands or shortcuts.
- Page content changes.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-window-navigation-surface.md`

## API, schema and event changes

- No external API changes.
- No schema or migration changes.
- Desktop window composition gains simpler top-level navigation bindings.

## Implementation sequence

1. Add top-level navigation properties to `DesktopShellWindowViewModel`.
2. Forward selected-section change notifications.
3. Extend focused tests.
4. Run desktop tests when tooling is available.

## Testing strategy

- Extend `DesktopShellWindowViewModelTests` for navigation exposure.
- Extend `ShellBindingNotificationTests` for current-section notifications.
- Run `Sockseek.Desktop.Tests` when `dotnet` is available.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- No secrets or persistence changes.
- No license impact.
- This is a UI-binding ergonomics improvement only.

## Risks and stop conditions

- Stop if the window should intentionally bind through nested shell view-models only.
- Stop if a broader top-level shell state aggregation model is planned instead.

## Acceptance-criteria mapping

- Strengthens the Avalonia app shell deliverable by simplifying sidebar binding at the window layer.
- Preserves the existing navigation model and keyboard shortcut behavior.
