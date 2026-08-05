# Sprint 4 window copy-diagnostics command bridge

## Goal

Expose a small window-level bridge for the shell diagnostics action so future Avalonia UI can trigger copy-diagnostics behavior through the window view-model instead of reaching into lower layers.

## Current-state findings

- `DesktopShellWindowViewModel` already exposes safe `DiagnosticsText` and diagnostics action metadata.
- The backend banner states can advertise when copy-diagnostics should be offered.
- There is no single window-level method representing the diagnostics action itself.

## In scope

- Add a window-level method that returns the current safe diagnostics text when the action is available.
- Keep the method side-effect free; clipboard integration remains out of scope.
- Add focused tests for available and unavailable states.

## Out of scope

- Actual clipboard integration.
- UI button wiring.
- Persisting or exporting diagnostics files.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-window-copy-diagnostics-command.md`

## API, schema and event changes

- No API changes.
- No schema or migration changes.
- Desktop-only view-model surface addition.

## Implementation sequence

1. Add a small diagnostics action bridge on the window view-model.
2. Add focused tests for false/null and successful text return paths.
3. Run desktop tests.

## Testing strategy

- Extend `DesktopShellWindowViewModelTests` for unavailable and available diagnostics action results.
- Run `Sockseek.Desktop.Tests` in Docker.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- Reuses the existing safe diagnostics export.
- Must continue to exclude session tokens.
- No license impact.

## Risks and stop conditions

- Stop if diagnostics action handling should wait for a broader UI command abstraction.
- Stop if copy/export semantics need a product decision before exposing a bridge.

## Acceptance-criteria mapping

- Supports the UX requirement for copyable diagnostics in error states.
- Keeps recovery/support actions inside the desktop shell window layer.
