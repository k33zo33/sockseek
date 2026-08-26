# Sprint 4 status icon accessibility labels

## Goal

Add localization-ready screen-reader labels for backend status icons so the desktop shell does not communicate status by color/icon alone.

## Current-state findings

- `BackendStatusBannerViewModel` already exposes visual status tokens and text copy.
- The UI/UX spec requires screen-reader labels for status icons and explicitly says status must not be conveyed only by color.
- The current banner model has no accessibility label metadata for its icon.

## In scope

- Add backend status icon accessibility label resources.
- Expose icon accessibility label text and resource keys from `BackendStatusBannerViewModel`.
- Cover the mapping with focused desktop tests.

## Out of scope

- Avalonia automation peers or runtime screen-reader integration.
- Broader accessibility metadata for every desktop control.
- Non-English translations.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-status-icon-accessibility-labels.md`

## API, schema and event changes

- No API changes.
- No schema or migration changes.
- Desktop-only accessibility metadata additions.

## Implementation sequence

1. Add status icon accessibility strings.
2. Expose the label metadata from backend banner view-models.
3. Extend tests for expected mappings.
4. Run desktop tests.

## Testing strategy

- Extend shell navigation tests to assert status icon accessibility keys/labels.
- Run `Sockseek.Desktop.Tests` in Docker.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- No secrets or persistence changes.
- No license impact.
- Pure accessibility scaffolding.

## Risks and stop conditions

- Stop if accessibility metadata needs a broader framework-level design first.
- Stop if this unexpectedly requires introducing actual Avalonia view code.

## Acceptance-criteria mapping

- Strengthens the UI/UX requirement that status is not conveyed by color alone.
- Improves Sprint 4 shell readiness for accessible desktop presentation.
