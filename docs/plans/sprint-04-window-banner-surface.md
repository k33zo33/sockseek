# Sprint 4 window banner surface

## Goal

Expose backend banner presentation metadata directly on `DesktopShellWindowViewModel` so a future Avalonia shell can bind top-level status UX without reaching through the nested `StatusBanner` model for common title, message, visibility, and icon surfaces.

## Current-state findings

- `DesktopShellWindowViewModel` already exposes the nested `StatusBanner` object plus a few action-oriented banner helpers.
- Future top-level shell markup would still need to drill into `StatusBanner` for the common banner title/message/icon bindings.
- The window view-model already provides similar top-level pass-throughs for current page and backend state.

## In scope

- Add top-level backend banner title, message, visibility, resource-key, and icon metadata properties.
- Forward property-changed notifications when the banner changes.
- Extend focused tests and binding-notification coverage.

## Out of scope

- Avalonia markup or actual banner rendering.
- New backend states or diagnostics behavior.
- Generic aggregation/refactor of all banner actions.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-window-banner-surface.md`

## API, schema and event changes

- No external API changes.
- No schema or migration changes.
- Desktop window composition gains simpler top-level banner bindings.

## Implementation sequence

1. Add top-level banner metadata properties to `DesktopShellWindowViewModel`.
2. Forward property-change notifications on banner updates.
3. Extend focused tests.
4. Run desktop tests when tooling is available.

## Testing strategy

- Extend `DesktopShellWindowViewModelTests` for initial and changed banner metadata.
- Extend `ShellBindingNotificationTests` for window-level banner metadata notifications.
- Run `Sockseek.Desktop.Tests` when `dotnet` is available.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- No secrets or persistence changes.
- No license impact.
- This is a UI-binding ergonomics improvement only.

## Risks and stop conditions

- Stop if the window should intentionally expose only the nested banner object.
- Stop if a broader shell snapshot model is planned instead.

## Acceptance-criteria mapping

- Strengthens the Sprint 4 backend starting/restarting/disconnected UX deliverable by simplifying window-level banner binding.
- Preserves the existing backend banner behavior and diagnostics actions.
