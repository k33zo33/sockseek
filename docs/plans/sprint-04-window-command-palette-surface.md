# Sprint 4 window command-palette surface

## Goal

Expose the command-palette model itself as a first-class window-level surface so future Avalonia shell composition can bind top-level overlay UI through `DesktopShellWindowViewModel` without reaching through nested shell state.

## Current-state findings

- `DesktopShellWindowViewModel` now forwards command-palette actions and open-state changes.
- Callers still need `Shell.CommandPalette` to access the palette model itself.
- Other shell chrome surfaces like `StatusBanner`, `PlayerBar`, and `CurrentPage` are already exposed at the window layer.

## In scope

- Add a window-level `CommandPalette` surface property.
- Extend focused tests to verify the top-level surface exposure.

## Out of scope

- Avalonia view or overlay markup.
- New command-palette item types.
- Search/filter state within the palette.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-window-command-palette-surface.md`

## API, schema and event changes

- No API changes.
- No persistence changes.
- Desktop shell composition becomes more uniform at the window layer.

## Implementation sequence

1. Expose the command-palette view-model from the window view-model.
2. Extend focused window tests.
3. Run desktop tests.

## Testing strategy

- Extend `DesktopShellWindowViewModelTests` for top-level palette-surface exposure.
- Run `Sockseek.Desktop.Tests` in Docker.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- No secret handling changes.
- No license impact.
- Improves future Avalonia shell composition ergonomics.

## Risks and stop conditions

- Stop if the window layer should deliberately avoid surfacing nested overlay models.
- Stop if a different top-level shell composition contract is planned.

## Acceptance-criteria mapping

- Strengthens the Avalonia desktop shell composition deliverable.
- Keeps top-level shell surfaces consistent across page, banner, player, and palette state.
