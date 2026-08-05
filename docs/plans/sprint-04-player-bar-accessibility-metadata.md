# Sprint 4 player bar accessibility metadata

## Goal

Add localization-ready accessibility labels and shortcut metadata to the persistent bottom player placeholder so the Sprint 4 shell scaffold is ready for accessible player controls.

## Current-state findings

- `PlayerBarPlaceholderViewModel` currently exposes placeholder copy and design tokens.
- The UI/UX spec defines a persistent bottom player with transport and queue controls.
- The placeholder model does not yet expose accessibility labels or keyboard-hint metadata for those controls.

## In scope

- Add player control accessibility label and hint resources.
- Expose the metadata from `PlayerBarPlaceholderViewModel`.
- Extend shell tests to verify the placeholder metadata.

## Out of scope

- Real playback behavior.
- Media key handling implementation.
- Avalonia button/view code.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-player-bar-accessibility-metadata.md`

## API, schema and event changes

- No API changes.
- No schema or migration changes.
- Desktop-only accessibility metadata additions.

## Implementation sequence

1. Add player-bar accessibility/hint strings.
2. Expose the metadata from the placeholder view-model.
3. Extend shell tests.
4. Run desktop tests.

## Testing strategy

- Extend shell navigation tests for player placeholder accessibility metadata.
- Run `Sockseek.Desktop.Tests` in Docker.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- No secrets or persistence changes.
- No license impact.
- Pure desktop accessibility scaffolding.

## Risks and stop conditions

- Stop if placeholder control metadata requires broader playback API decisions.
- Stop if this unexpectedly forces actual view implementation.

## Acceptance-criteria mapping

- Strengthens the persistent bottom player placeholder deliverable.
- Supports the keyboard accessibility expectations in the desktop UX spec.
