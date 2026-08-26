# Sprint 4 window player controls surface

## Goal

Expose persistent player-control placeholder metadata directly on `DesktopShellWindowViewModel` so a future Avalonia shell can bind the bottom-player controls from the top-level window model without drilling into the nested `PlayerBar` object for transport, queue, volume, and expanded-player chrome.

## Current-state findings

- `DesktopShellWindowViewModel` already exposes top-level player-bar title/artwork/artist/progress summary metadata.
- Control metadata still requires bindings to reach through `PlayerBar` for icon tokens, accessibility labels, hints, and availability flags.
- The current player placeholder model is static, so top-level pass-throughs are a safe foundation slice.

## In scope

- Add top-level previous/play-pause/next/queue/volume/expanded-player metadata properties to `DesktopShellWindowViewModel`.
- Reuse the existing placeholder metadata only; do not add playback behavior.
- Extend focused tests for default placeholder exposure.

## Out of scope

- Real playback state or player commands.
- Avalonia markup or actual bottom-bar rendering.
- Dynamic player-state notifications beyond the current static placeholder model.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-window-player-controls-surface.md`

## API, schema and event changes

- No external API changes.
- No schema or migration changes.
- Desktop window composition gains simpler top-level player-control bindings.

## Implementation sequence

1. Add top-level player-control metadata properties to `DesktopShellWindowViewModel`.
2. Extend focused tests for default placeholder exposure.
3. Run desktop tests when tooling is available.

## Testing strategy

- Extend `DesktopShellWindowViewModelTests` for top-level player-control metadata.
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

- Strengthens the persistent bottom player placeholder deliverable by simplifying top-level binding of the documented control strip.
- Preserves the current static Sprint 4 placeholder behavior.
