# Sprint 4 command palette shortcut toggle

## Goal

Tighten command-palette keyboard behavior so the shell scaffold feels more like a real desktop shell: `Ctrl+K` should toggle the palette, and direct navigation should dismiss it.

## Current-state findings

- `CommandPaletteViewModel` already has `Toggle()`.
- `ShellNavigationViewModel.TryHandleShortcut()` currently always opens the palette for `Ctrl+K`.
- Direct navigation through shortcuts or `NavigateTo()` can leave the palette open.

## In scope

- Toggle the command palette with `Ctrl+K`.
- Close the palette when navigating to a section.
- Extend focused headless and view-model tests.

## Out of scope

- Search/filter text inside the command palette.
- Avalonia view wiring.
- Global Escape-key handling.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-command-palette-shortcut-toggle.md`

## API, schema and event changes

- No API changes.
- No persistence changes.
- Desktop shell keyboard/navigation behavior becomes more coherent.

## Implementation sequence

1. Make `Ctrl+K` toggle the palette.
2. Dismiss the palette on section navigation.
3. Extend focused tests.
4. Run desktop tests.

## Testing strategy

- Extend `ShellNavigationHeadlessTests` for toggle and dismiss semantics.
- Extend `ShellNavigationViewModelTests` for shortcut toggle behavior.
- Run `Sockseek.Desktop.Tests` in Docker.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- No secret handling changes.
- No license impact.
- Improves keyboard accessibility and shell UX consistency.

## Risks and stop conditions

- Stop if future palette search state requires remaining open across navigation.
- Stop if shell navigation is expected to preserve transient overlays.

## Acceptance-criteria mapping

- Strengthens the command-palette skeleton deliverable.
- Improves keyboard-driven navigation across the main shell sections.
