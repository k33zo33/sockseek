# Sprint 4 player bar transport icon tokens

## Goal

Complete the player placeholder icon metadata by adding icon tokens for the previous, play/pause, and next transport controls.

## Current-state findings

- `PlayerBarPlaceholderViewModel` already exposes icon tokens for queue, volume, and expanded-player controls.
- The transport controls still expose only accessibility labels/hints and no icon tokens.
- This leaves the persistent player placeholder inconsistent across its documented controls.

## In scope

- Add desktop design tokens for previous, play/pause, and next player icons.
- Expose those icon tokens from `PlayerBarPlaceholderViewModel`.
- Extend shell tests to verify the new tokens.

## Out of scope

- Actual playback implementation.
- Media-key behavior or transport command wiring.
- Visual asset implementation beyond token names.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-player-bar-transport-icon-tokens.md`

## API, schema and event changes

- No API changes.
- No schema or migration changes.
- Desktop-only placeholder metadata additions.

## Implementation sequence

1. Add transport icon design tokens.
2. Expose the tokens from `PlayerBarPlaceholderViewModel`.
3. Extend shell tests.
4. Run desktop tests.

## Testing strategy

- Extend `ShellNavigationViewModelTests` for the new transport icon tokens.
- Run `Sockseek.Desktop.Tests` in Docker.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- No secret handling changes.
- No license impact.
- Improves placeholder consistency for the desktop shell.

## Risks and stop conditions

- Stop if token naming needs a broader design-token decision first.
- Stop if this implies real transport behavior beyond Sprint 4.

## Acceptance-criteria mapping

- Strengthens the persistent bottom player placeholder deliverable.
- Keeps player placeholder metadata uniform across the full documented control strip.
