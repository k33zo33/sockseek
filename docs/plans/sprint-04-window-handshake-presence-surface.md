# Sprint 4 window handshake presence surface

## Goal

Expose secure-session presence as a first-class window-level shell property so future Avalonia bindings can reason about backend session availability without null-checking the handshake object directly.

## Current-state findings

- `DesktopShellWindowViewModel` now exposes `CurrentHandshake`.
- Top-level callers still need to check `CurrentHandshake is not null` to know if a secure session exists.
- Other shell UI decisions often benefit from a simple boolean binding surface.

## In scope

- Add `HasCurrentHandshake` to the window view-model.
- Forward property-change notifications when handshake presence changes.
- Extend focused tests.

## Out of scope

- Changes to handshake parsing or daemon behavior.
- New API or SignalR behavior.
- Avalonia markup.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-window-handshake-presence-surface.md`

## API, schema and event changes

- No API changes.
- No persistence changes.
- Desktop shell composition gets a simpler top-level session-availability surface.

## Implementation sequence

1. Add `HasCurrentHandshake` to the window view-model.
2. Forward notifications on handshake changes.
3. Extend focused tests.
4. Run desktop tests.

## Testing strategy

- Extend `DesktopShellWindowViewModelTests` for handshake-presence exposure.
- Extend `ShellBindingNotificationTests` for handshake-presence notifications.
- Run `Sockseek.Desktop.Tests` in Docker.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- No secret handling changes.
- No license impact.
- Improves binding ergonomics without exposing extra secret data.

## Risks and stop conditions

- Stop if top-level bindings should intentionally use the full handshake object only.
- Stop if a broader aggregated backend-status model is planned instead.

## Acceptance-criteria mapping

- Strengthens the desktop shell composition deliverable.
- Simplifies top-level backend session state binding for future Avalonia UI.
