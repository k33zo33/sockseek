# Sprint 4 window page surface

## Goal

Expose current-page presentation metadata directly on `DesktopShellWindowViewModel` so a future Avalonia shell can bind the main content header and placeholder copy without drilling into nested page objects for the common title, description, and icon surfaces.

## Current-state findings

- `DesktopShellWindowViewModel` currently exposes `CurrentPage`, but consumers must bind through that nested object for common page-header values.
- `ShellPageViewModel` already owns the authoritative page title, description, resource keys, and icon token.
- The window view-model already follows this pattern for other top-level shell chrome properties such as backend state and command-palette state.

## In scope

- Add window-level current-page title, description, resource-key, and icon-token properties.
- Forward property-changed notifications when the current page changes.
- Extend focused tests and binding-notification coverage.

## Out of scope

- Avalonia markup or actual page header rendering.
- New pages, navigation shortcuts, or content behavior.
- Runtime localization switching.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-window-page-surface.md`

## API, schema and event changes

- No external API changes.
- No schema or migration changes.
- Desktop window composition gains simpler page-header binding surfaces.

## Implementation sequence

1. Add top-level current-page metadata properties to `DesktopShellWindowViewModel`.
2. Forward change notifications on page switches.
3. Extend focused tests.
4. Run desktop tests when tooling is available.

## Testing strategy

- Extend `DesktopShellWindowViewModelTests` for initial and navigated page metadata.
- Extend `ShellBindingNotificationTests` for window-level page metadata notifications.
- Run `Sockseek.Desktop.Tests` when `dotnet` is available.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- No secrets or persistence changes.
- No license impact.
- This is a UI-binding ergonomics improvement only.

## Risks and stop conditions

- Stop if the window should intentionally expose only the nested page object.
- Stop if a broader aggregated page-header model is planned instead.

## Acceptance-criteria mapping

- Strengthens the Avalonia app shell deliverable by simplifying binding of the active page header/content metadata.
- Preserves the existing placeholder-page navigation model.
