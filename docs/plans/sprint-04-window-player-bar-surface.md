# Sprint 4 window player-bar surface

## Goal

Expose core persistent player-bar placeholder metadata directly on `DesktopShellWindowViewModel` so a future Avalonia shell can bind top-level bottom-chrome content without drilling into the nested `PlayerBar` model for common title, artwork, artist, progress, and queue-summary surfaces.

## Current-state findings

- `DesktopShellWindowViewModel` already exposes the nested `PlayerBar` object.
- Future top-level shell markup would still need to bind through `PlayerBar` for the common persistent-bottom-player content.
- The window view-model already provides similar top-level pass-throughs for page and backend banner metadata.

## In scope

- Add top-level player-bar title, artwork, artist, progress, queue-summary, and surface-token properties.
- Reuse the existing placeholder metadata; do not introduce playback behavior.
- Extend focused tests for the new top-level surface.

## Out of scope

- Real playback state or player commands.
- Avalonia markup or actual bottom-bar rendering.
- Dynamic player-state notifications beyond the current static placeholder model.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-window-player-bar-surface.md`

## API, schema and event changes

- No external API changes.
- No schema or migration changes.
- Desktop window composition gains simpler top-level player-bar bindings.

## Implementation sequence

1. Add top-level player-bar metadata properties to `DesktopShellWindowViewModel`.
2. Extend focused tests for default placeholder exposure.
3. Run desktop tests when tooling is available.

## Testing strategy

- Extend `DesktopShellWindowViewModelTests` for top-level player-bar metadata.
- Run `Sockseek.Desktop.Tests` when `dotnet` is available.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- No secrets or persistence changes.
- No license impact.
- This is a UI-binding ergonomics improvement only.

## Risks and stop conditions

- Stop if the window should intentionally expose only the nested player-bar object.
- Stop if a broader future player snapshot model should land first.

## Acceptance-criteria mapping

- Strengthens the persistent bottom player placeholder deliverable by simplifying window-level binding of the shell chrome.
- Preserves the current static Sprint 4 placeholder behavior.
