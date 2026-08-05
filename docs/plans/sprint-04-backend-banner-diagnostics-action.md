# Sprint 4 backend banner diagnostics action metadata

## Goal

Make backend status banners actionable by exposing localization-ready metadata for a copy-diagnostics action when the shell is in a recoverable problem state.

## Current-state findings

- The shell now supports safe diagnostics export from `DesktopShellWindowViewModel`.
- `BackendStatusBannerViewModel` currently exposes only informational banner copy and icon metadata.
- The UI/UX spec says error states should offer copyable diagnostics instead of a dead-end generic error.

## In scope

- Add copy-diagnostics action resource strings.
- Expose banner action visibility, label, and hint metadata from `BackendStatusBannerViewModel`.
- Wire the metadata for backend states that should surface diagnostics.
- Extend tests for expected action availability.

## Out of scope

- Actual button rendering.
- Clipboard integration.
- Diagnostics for future playlist/download/player workflows.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-backend-banner-diagnostics-action.md`

## API, schema and event changes

- No API changes.
- No schema or migration changes.
- Desktop-only UX metadata additions.

## Implementation sequence

1. Add copy-diagnostics resource strings.
2. Extend backend banner view-model metadata.
3. Wire action visibility for relevant backend states.
4. Run desktop tests.

## Testing strategy

- Extend shell navigation tests for banner action resource keys and visibility.
- Run `Sockseek.Desktop.Tests` in Docker.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- Reuses the existing safe diagnostics export.
- No secrets or tokens are added to banner metadata.
- No license impact.

## Risks and stop conditions

- Stop if banner action metadata needs a broader command/action framework first.
- Stop if diagnostics action semantics need product decisions beyond current Sprint 4 shell scope.

## Acceptance-criteria mapping

- Strengthens disconnected/unauthorized/restarting UX.
- Supports the UI/UX requirement for copyable diagnostics in error states.
