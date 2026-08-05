# Sprint 4 shell window view-model

## Goal

Add a window-level desktop shell view-model that composes the existing shell session state into a real top-level desktop chrome model without pulling in premature Avalonia/XAML implementation.

## Current-state findings

- `DesktopShellSession` already composes supervisor, recovery coordinator, and navigation shell state.
- The project still lacks a top-level desktop window model representing the app title, current page heading, backend banner, theme, and persistent player placeholder as one object.
- This leaves the Sprint 4 shell state reusable but not yet shaped like a real window surface.

## In scope

- Add `DesktopShellWindowViewModel` that wraps `DesktopShellSession`.
- Expose app title/resource metadata and top-level accessors for shell state.
- Add focused tests for default state and navigation/theme updates.

## Out of scope

- Avalonia `Application`, `Window`, or XAML files.
- Runtime property change notification.
- Additional UI pages beyond current placeholders.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-shell-window-view-model.md`

## API, schema and event changes

- No API changes.
- No schema or migration changes.
- Desktop-only composition model additions.

## Implementation sequence

1. Add a window-level shell view-model.
2. Add a default app title resource.
3. Add focused unit tests.
4. Run desktop tests.

## Testing strategy

- Add view-model tests for default shell window state and navigation/theme reflection.
- Run `Sockseek.Desktop.Tests` in Docker.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- No secrets or network changes.
- No license impact.
- Pure desktop composition scaffolding.

## Risks and stop conditions

- Stop if useful shell window behavior requires Avalonia-specific binding infrastructure first.
- Stop if a top-level window model conflicts with a locked architecture decision.

## Acceptance-criteria mapping

- Strengthens the `Avalonia app shell, sidebar, routing i design tokeni` deliverable.
- Preserves current placeholder-page and player-bar Sprint 4 scope.
