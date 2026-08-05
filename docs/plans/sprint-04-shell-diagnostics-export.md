# Sprint 4 shell diagnostics export

## Goal

Add a copyable desktop shell diagnostics snapshot so backend-state UX can offer actionable diagnostics without exposing secrets.

## Current-state findings

- The desktop shell now has bindable window/shell state, backend banner state, and connection handshake state.
- The UI/UX spec says error states should provide an action to copy diagnostics instead of a dead-end generic error.
- There is currently no shared desktop model for exporting a safe user-facing diagnostics summary.

## In scope

- Add a diagnostics snapshot model for current shell/window/backend state.
- Add a formatter/export method from the shell window view-model.
- Ensure diagnostics never expose the session token.
- Add focused unit tests for connected and disconnected diagnostics output.

## Out of scope

- Clipboard integration.
- Correlation IDs or server-generated diagnostics beyond current Sprint 4 shell state.
- Future workflow/download/player diagnostics.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-shell-diagnostics-export.md`

## API, schema and event changes

- No API changes.
- No schema or migration changes.
- Desktop-only diagnostics model additions.

## Implementation sequence

1. Add diagnostics snapshot/export models.
2. Expose snapshot/export from the shell window view-model.
3. Add focused tests for safe output.
4. Run desktop tests.

## Testing strategy

- Add window-level tests for diagnostics snapshot and formatted export.
- Explicitly assert that session tokens are omitted.
- Run `Sockseek.Desktop.Tests` in Docker.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- Diagnostics must not include session tokens or secrets.
- No license impact.
- Improves supportability of local desktop UX.

## Risks and stop conditions

- Stop if useful diagnostics require future-sprint workflow or player state decisions.
- Stop if diagnostics content needs a broader privacy policy decision.

## Acceptance-criteria mapping

- Strengthens the backend disconnected/unauthorized UX requirement.
- Supports the UI/UX requirement for copyable diagnostics in error states.
