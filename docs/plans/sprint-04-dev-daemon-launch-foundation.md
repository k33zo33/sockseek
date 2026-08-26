# Sprint 4 development daemon launch foundation

## Goal

Add the smallest desktop-side Sprint 4 foundation needed to launch a local development daemon through the existing supervisor abstraction.

## Current-state findings

- `Sockseek.Desktop` already has `DesktopDaemonSupervisor`, handshake parsing, navigation placeholders, and backend-state banner state.
- `Sockseek.Server` now supports an explicit opt-in startup handshake for desktop-supervised launches.
- The desktop project still lacks a concrete `IDesktopProcessLauncher` implementation and a canonical way to build a development daemon launch request.
- `Program.cs` is still a stub, so this slice should stay below full app bootstrap and focus on reusable launch primitives.

## In scope

- Add a concrete local process launcher for `IDesktopProcessLauncher`.
- Add a small factory that builds a development daemon `DesktopDaemonLaunchRequest`.
- Ensure the request opts into the server startup handshake without changing ordinary daemon behavior.
- Add focused desktop tests for request construction and process output capture.

## Out of scope

- Full Avalonia application startup.
- Single-instance enforcement.
- SignalR reconnect manager.
- Theme persistence.
- Packaged/self-contained daemon discovery.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-dev-daemon-launch-foundation.md`

## API, schema and event changes

- No HTTP or SignalR contract changes.
- No persistence or schema changes.
- No public API changes outside the desktop internal launch abstraction surface already in the repo.

## Implementation sequence

1. Inspect the existing `DesktopDaemonLaunchRequest` and `IDesktopProcessLauncher` contracts.
2. Add a development launch-request factory that targets `Sockseek.Server` and enables handshake stdout.
3. Add a concrete `System.Diagnostics.Process` launcher that redirects output for handshake parsing.
4. Add focused tests for request contents and line-oriented output capture.
5. Run targeted desktop tests.

## Testing strategy

- `Sockseek.Desktop.Tests` targeted test run in Docker.
- Re-run existing desktop supervisor/parser tests as regression coverage.
- Keep tests OS-portable by using shell commands only for simple output-capture verification.

## Migration and rollback

- No migration.
- Rollback is a normal code revert with no persisted state changes.

## Security, privacy and license impact

- Keeps the daemon handshake opt-in and local to desktop-supervised launches.
- Does not introduce provider playback or secret persistence changes.
- No license impact.

## Risks and stop conditions

- Stop if the launch request would require hard-coding a packaged path that is not stable yet.
- Stop if the launcher abstraction cannot stay testable without introducing UI framework coupling.
- Stop if child-process behavior would expose session tokens outside the explicit handshake channel.

## Acceptance-criteria mapping

- Contributes to “user does not have to manually start daemon”.
- Contributes to “DesktopDaemonSupervisor and secure session handshake”.
- Preserves the current shell/navigation placeholder work without expanding into future sprint UI features.
