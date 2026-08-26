# Sprint 4 window diagnostics binding

## Goal

Expose bindable window-level diagnostics state so future desktop shell UI can wire a copy-diagnostics action without duplicating backend or handshake logic.

## Current-state findings

- `DesktopShellWindowViewModel` can generate diagnostics text on demand.
- Backend banner states now expose copy-diagnostics action metadata.
- The window view-model does not currently surface that action metadata or notify bindings when handshake details change.

## In scope

- Add bindable window-level diagnostics action properties derived from the backend banner.
- Add a bindable diagnostics text property derived from current shell/window state.
- Raise property changes when navigation, theme, backend state, banner state, or handshake state changes.
- Extend unit tests to cover reactive updates.

## Out of scope

- Clipboard integration.
- Actual Avalonia button wiring.
- Additional diagnostics fields beyond current Sprint 4 shell state.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-window-diagnostics-binding.md`

## API, schema and event changes

- No API changes.
- No schema or migration changes.
- Desktop-only view-model surface additions.

## Implementation sequence

1. Add window-level bindable diagnostics properties.
2. Wire property change propagation for all diagnostics dependencies.
3. Add focused tests for default and reactive updates.
4. Run desktop tests.

## Testing strategy

- Extend `DesktopShellWindowViewModelTests` for diagnostics action availability and property updates.
- Keep `DesktopShellDiagnosticsSnapshotTests` as formatting/privacy coverage.
- Run `Sockseek.Desktop.Tests` in Docker.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- Reuses the existing safe diagnostics export.
- Does not expose session tokens or new secrets.
- No license impact.

## Risks and stop conditions

- Stop if a broader shell command framework is needed before exposing the action.
- Stop if diagnostics state must move to a different composition root first.

## Acceptance-criteria mapping

- Strengthens backend starting/restarting/disconnected UX.
- Supports copyable diagnostics from problem states with bindable shell metadata.
