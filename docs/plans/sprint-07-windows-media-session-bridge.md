# Sprint 7 Windows media session bridge

## Goal

Add Windows-target media key handling for the desktop player so play/pause, previous, next and mute can reach the local player controls through a platform bridge rather than only normal focused control key routing.

## Current-state findings

- `DesktopShellKeyRouting` already maps Avalonia `Key.MediaPlayPause`, `Key.MediaPreviousTrack`, `Key.MediaNextTrack`, `Key.VolumeMute` and non-text-input Space into `DesktopPlayerInput`.
- `DesktopShellMainWindow` currently handles only normal `KeyDown` events raised by the Avalonia window.
- Sprint 7 requires media keys on the Windows target, and `UI_UX.md` says media keys should work independent of focus.
- No Windows-specific global media key bridge, native hotkey registration or media session abstraction exists yet.
- The player controls already flow through `PlayerBarPlaceholderViewModel.TryHandleInput`, which calls the typed local player API.

## In scope

- Add a small desktop-side media key bridge abstraction.
- Add a Windows implementation that can register/unregister the supported media keys without affecting non-Windows targets.
- Route bridge events into the same `DesktopPlayerInput` path used by existing key routing.
- Add focused unit tests around registration lifecycle, input mapping and disposal behavior using a fake registrar.
- Document the bridge scope and limitations.

## Out of scope

- Provider playback, provider audio URLs or external media sources.
- OS now-playing metadata publishing beyond the key bridge.
- Native package or installer changes.
- Linux/macOS media key implementations.
- Replacing the existing Avalonia key routing path.

## Files and projects affected

- `Sockseek.Desktop`: media key bridge abstraction/implementation and window lifetime wiring.
- `Sockseek.Desktop.Tests`: fake bridge/registrar tests.
- `docs/plans`: this plan.

## API, schema and event changes

- No daemon HTTP API change.
- No database schema change.
- No SignalR event change.
- Desktop-only internal abstraction added.

## Implementation sequence

1. Extract a testable `IDesktopMediaKeyBridge`/registrar shape that emits `DesktopPlayerInput`.
2. Implement a no-op bridge for unsupported platforms.
3. Implement the Windows bridge behind `OperatingSystem.IsWindows()` using native registration only when the main window is active enough to provide a platform handle.
4. Wire the bridge into `DesktopShellMainWindow` lifecycle and route events to `DesktopShellKeyRouting.TryHandleShellInput`.
5. Add unit tests for mapping, registration failure tolerance, unregister-on-dispose and duplicate event suppression if needed.
6. Run Desktop targeted tests, full Release build and full test suite.

## Testing strategy

- Pure unit tests cover bridge-to-player-input routing through fakes.
- Existing key routing tests remain for Avalonia focused/window key events.
- Full Desktop test suite verifies no regressions in window/view-model composition.

## Migration and rollback

- No migration.
- Rollback removes the desktop bridge files and window lifetime wiring.

## Security, privacy and license impact

- Handles only local OS media key events and local player commands.
- Does not expose secrets, local paths, provider tokens or provider audio.
- No license impact unless an external native binding package is introduced; prefer BCL P/Invoke first.

## Risks and stop conditions

- Stop if implementation requires adding a Windows runtime/package that changes packaging or license posture without review.
- Stop if Avalonia does not expose a stable message hook/handle path in this repo without adding a new dependency.
- Treat registration failure as non-fatal; app-window key routing must continue working.

## Acceptance-criteria mapping

- Media keys work on Windows target: covered by Windows bridge plus fake registrar tests and existing focused key routing tests.
- Player never attempts provider audio URL: unchanged and still covered by player tests.
