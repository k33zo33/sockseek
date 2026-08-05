# Sprint 4 player bar control icon tokens

## Goal

Complete the player placeholder metadata by adding icon tokens for the volume and expanded-player controls so the documented shell chrome has consistent visual metadata across the player strip.

## Current-state findings

- `PlayerBarPlaceholderViewModel` already exposes a queue icon token.
- Volume and expanded-player placeholder controls now have labels and hints, but no icon tokens.
- This leaves the player scaffold internally inconsistent even though the UX spec lists those controls.

## In scope

- Add desktop design tokens for player volume and expanded-player icons.
- Expose those icon tokens from the player placeholder view-model.
- Extend shell tests to verify the new tokens.

## Out of scope

- Actual volume or expanded-player behavior.
- Additional player controls beyond the current Sprint 4 placeholder.
- Visual asset implementation.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-player-bar-control-icon-tokens.md`

## API, schema and event changes

- No API changes.
- No schema or migration changes.
- Desktop-only metadata additions.

## Implementation sequence

1. Add player control icon design tokens.
2. Expose the tokens from `PlayerBarPlaceholderViewModel`.
3. Extend shell tests.
4. Run desktop tests.

## Testing strategy

- Extend `ShellNavigationViewModelTests` for the new icon tokens.
- Run `Sockseek.Desktop.Tests` in Docker.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- No secret handling changes.
- No license impact.
- Improves shell placeholder consistency.

## Risks and stop conditions

- Stop if icon token naming needs a broader design-token decision first.
- Stop if this would imply visual asset work beyond Sprint 4.

## Acceptance-criteria mapping

- Strengthens the persistent bottom player placeholder deliverable.
- Keeps shell metadata aligned with the documented player control strip.
