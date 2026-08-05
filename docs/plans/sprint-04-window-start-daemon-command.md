# Sprint 4 window start-daemon command bridge

## Goal

Expose a small window-level async bridge for starting the local daemon so future Avalonia shell UI can trigger daemon recovery through the existing session orchestration path.

## Current-state findings

- `DesktopShellWindowViewModel` now exposes start-daemon action metadata and availability.
- `DesktopShellSession` already owns the actual daemon startup logic through `StartAsync`.
- Without a window-level bridge, future UI code would have to reach around the view-model to trigger session startup.

## In scope

- Add `TryStartDaemonAsync` to the window view-model.
- Delegate directly to the existing session startup logic.
- Add focused tests for unavailable and successful launch paths.

## Out of scope

- Clipboard or command framework work.
- Busy-state tracking or retry throttling.
- New daemon launch semantics beyond existing session behavior.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-window-start-daemon-command.md`

## API, schema and event changes

- No API changes.
- No schema or migration changes.
- Desktop-only view-model surface addition.

## Implementation sequence

1. Add the window-level async bridge.
2. Add focused tests for false/true results.
3. Run desktop tests.

## Testing strategy

- Extend `DesktopShellWindowViewModelTests` for unavailable and successful daemon launch calls.
- Run `Sockseek.Desktop.Tests` in Docker.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- Reuses the existing local daemon launch/session handshake path.
- No new secret handling.
- No license impact.

## Risks and stop conditions

- Stop if command execution should move to a broader action framework first.
- Stop if busy-state UX must be designed before exposing the method.

## Acceptance-criteria mapping

- Strengthens the local-daemon startup/recovery UX.
- Keeps daemon lifecycle orchestration inside the desktop shell/session layer.
