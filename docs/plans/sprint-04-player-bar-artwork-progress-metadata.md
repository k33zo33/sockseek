# Sprint 4 player bar artwork and progress metadata

## Goal

Complete the persistent player placeholder metadata with artwork and progress placeholder content so the bottom-player scaffold fully reflects the documented shell layout.

## Current-state findings

- The desktop UX spec explicitly lists artwork and progress as part of the persistent player.
- `PlayerBarPlaceholderViewModel` currently covers title/artist and the control strip metadata.
- Artwork and progress placeholder metadata are still missing.

## In scope

- Add shared string resources for artwork and progress placeholder metadata.
- Extend `PlayerBarPlaceholderViewModel` with artwork/progress labels and resource keys.
- Extend shell and string-resource tests.

## Out of scope

- Real playback progress.
- Album-art loading.
- Interactive seek behavior.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-player-bar-artwork-progress-metadata.md`

## API, schema and event changes

- No API changes.
- No schema or migration changes.
- Desktop-only placeholder metadata additions.

## Implementation sequence

1. Add artwork/progress resources.
2. Extend the player placeholder view-model.
3. Extend focused tests.
4. Run desktop tests.

## Testing strategy

- Extend `ShellNavigationViewModelTests` for player placeholder metadata.
- Extend `DesktopStringResourcesTests` with new known keys.
- Run `Sockseek.Desktop.Tests` in Docker.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- No secret handling changes.
- No license impact.
- Improves completeness of the desktop shell placeholder.

## Risks and stop conditions

- Stop if artwork/progress placeholders require broader player-state modeling first.
- Stop if this implies real playback behavior beyond Sprint 4.

## Acceptance-criteria mapping

- Strengthens the persistent bottom player placeholder deliverable.
- Keeps the shell scaffold aligned with the documented desktop UX.
