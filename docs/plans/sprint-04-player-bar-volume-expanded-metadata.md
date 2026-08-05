# Sprint 4 player bar volume and expanded-player metadata

## Goal

Complete the persistent player placeholder metadata so it covers the documented volume and expanded-player controls in addition to previous/play/next/queue.

## Current-state findings

- The desktop UX spec lists player controls for queue and expanded player, plus volume.
- `PlayerBarPlaceholderViewModel` already exposes localization-ready metadata for previous, play/pause, next, and queue.
- Volume and expanded-player affordances do not yet have placeholder metadata, leaving the shell scaffold incomplete.

## In scope

- Add shared string resources for volume and expanded-player labels/hints.
- Extend `PlayerBarPlaceholderViewModel` with localization-ready metadata for those controls.
- Extend shell tests to verify the new placeholder metadata.

## Out of scope

- Actual player implementation.
- Interactive volume or expanded-player behavior.
- Progress slider or queue contents.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-player-bar-volume-expanded-metadata.md`

## API, schema and event changes

- No API changes.
- No schema or migration changes.
- Desktop-only placeholder metadata additions.

## Implementation sequence

1. Add volume and expanded-player resource strings.
2. Extend the player placeholder view-model.
3. Extend focused shell tests.
4. Run desktop tests.

## Testing strategy

- Extend `ShellNavigationViewModelTests` for the new control metadata.
- Run `Sockseek.Desktop.Tests` in Docker.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- No secrets or token handling changes.
- No license impact.
- Improves shell completeness and accessibility metadata.

## Risks and stop conditions

- Stop if the control set needs a broader player UX decision first.
- Stop if adding these placeholders would imply real playback behavior beyond Sprint 4.

## Acceptance-criteria mapping

- Strengthens the persistent bottom player placeholder deliverable.
- Keeps the shell scaffold aligned with the documented desktop UX.
