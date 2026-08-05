# Sprint 4 window start-daemon action metadata

## Goal

Expose bindable window-level metadata for a start-local-daemon action so the desktop shell can surface actionable recovery UI without introducing actual button or command wiring yet.

## Current-state findings

- `DesktopProgramBootstrap` automatically starts the local daemon when launch prerequisites exist.
- `DesktopShellSession` already knows whether daemon launch is possible through `CanStartDaemon`.
- `DesktopShellWindowViewModel` does not currently expose any localization-ready action metadata for a manual start/relaunch affordance.

## In scope

- Add start-daemon label and hint resources.
- Expose window-level start-daemon action metadata derived from session and backend state.
- Keep availability limited to sensible manual-recovery states.
- Extend unit tests for default and reactive action availability.

## Out of scope

- Actual button rendering.
- Executing the start action from the view-model.
- New daemon launch behavior beyond existing session startup logic.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-window-start-daemon-action.md`

## API, schema and event changes

- No API changes.
- No schema or migration changes.
- Desktop-only view-model surface additions.

## Implementation sequence

1. Add localization-ready start-daemon strings.
2. Expose window-level start-daemon action properties.
3. Add tests for state-based availability.
4. Run desktop tests.

## Testing strategy

- Extend `DesktopShellWindowViewModelTests` to cover availability for connected and disconnected states.
- Run `Sockseek.Desktop.Tests` in Docker.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- Reuses existing local daemon launch rules.
- No new secret handling.
- No license impact.

## Risks and stop conditions

- Stop if a broader command/action framework is required before exposing metadata.
- Stop if manual relaunch semantics need product clarification beyond current shell scope.

## Acceptance-criteria mapping

- Strengthens the requirement that users should not need to manually manage backend state.
- Supports backend disconnected UX with an actionable recovery affordance.
