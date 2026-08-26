# Sprint 4 theme and command palette foundation

## Goal

Add the smallest desktop-side foundation for theme selection state and a command palette skeleton, including shortcut handling and preference storage abstractions.

## Current-state findings

- `Sockseek.Desktop` already has shell navigation placeholders, backend status UX, daemon supervision, authenticated API access, and SignalR reconnect scaffolding.
- The current shell does not yet handle the required `Ctrl+K` command palette shortcut.
- The current shell has no theme preference model or persistence abstraction, even though Sprint 4 expects light/dark theme memory.
- No localization-ready resource keys exist yet for shell placeholders or command palette labels.

## In scope

- Add a desktop theme preference enum and storage abstraction.
- Add a command palette skeleton view model with keyboard shortcut metadata.
- Extend `ShellNavigationViewModel` to load/save theme preference state and handle `Ctrl+K`.
- Add localization-ready resource keys to shell/page and command palette placeholders.
- Add focused unit tests for theme state, persistence abstraction, and command palette behavior.

## Out of scope

- Full Avalonia theme resources or style dictionaries.
- Real disk-backed preference persistence.
- Full command execution routing beyond the existing shell shortcuts.
- Full application bootstrap or settings UI composition.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-theme-command-palette-foundation.md`

## API, schema and event changes

- No server API changes.
- No database or migration changes.
- Desktop-only view-model and preference abstraction changes.

## Implementation sequence

1. Add theme preference and store abstractions.
2. Add command palette skeleton models with resource keys.
3. Update shell navigation to load/save theme preference and handle `Ctrl+K`.
4. Add unit tests for persisted theme state, command palette visibility, and shortcut behavior.
5. Run targeted desktop tests.

## Testing strategy

- Targeted `Sockseek.Desktop.Tests` run in Docker.
- Extend shell navigation tests instead of introducing broad new harnesses.
- Use an in-memory test store to verify preference persistence behavior.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- No secret handling changes.
- No network or provider-scope changes.
- No license impact.

## Risks and stop conditions

- Stop if theme persistence requires crossing the Desktop/Infrastructure boundary in a way that needs an ADR.
- Stop if command palette work starts forcing full Avalonia UI implementation in this slice.

## Acceptance-criteria mapping

- Contributes to “Light/dark tema se pamti” with a desktop preference abstraction.
- Contributes to “theme, localization-ready resources i command palette skeleton”.
- Preserves the existing shell/navigation placeholder scope.
