# Sprint 4 shell session composition

## Goal

Add a small desktop shell session composition object that wires together the daemon supervisor, recovery coordinator, shell navigation view model, and optional dev-daemon launch path.

## Current-state findings

- `ShellNavigationViewModel`, `DesktopDaemonSupervisor`, and `DesktopBackendRecoveryCoordinator` already exist and are individually tested.
- `Program` currently has only single-instance startup gating and no desktop-side object that represents a running shell session.
- The current foundations therefore remain loosely connected and require ad hoc wiring for future Avalonia bootstrap work.

## In scope

- Add a `DesktopShellSession` composition object.
- Compose supervisor, shell navigation, and backend recovery into one disposable session.
- Support optional `StartAsync()` launch of the development daemon from a workspace root.
- Add focused unit tests for launch success, missing launch context, and supervisor-driven state propagation through the session.

## Out of scope

- Full Avalonia `App` bootstrap.
- Real windows, views, or design-token styling.
- Installer/package composition.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-shell-session-composition.md`

## API, schema and event changes

- No server API changes.
- No schema or migration changes.
- Desktop-only composition changes.

## Implementation sequence

1. Add `DesktopShellSession` with composed supervisor, recovery coordinator, and shell view model.
2. Add optional `StartAsync` launch path using the existing dev daemon launch request factory.
3. Add focused tests around successful launch and state propagation.
4. Run desktop tests.

## Testing strategy

- Extend `Sockseek.Desktop.Tests` with focused session-composition tests using fake launcher outputs and fake event hub connections.
- Run targeted desktop tests in Docker.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- Reuses the existing loopback-only handshake and local session token path.
- No new secret storage.
- No license impact.

## Risks and stop conditions

- Stop if a useful shell session abstraction requires introducing real Avalonia application lifecycle pieces.
- Stop if launch wiring would need packaging/runtime decisions outside current Sprint 4 scope.

## Acceptance-criteria mapping

- Strengthens “Korisnik ne mora ručno pokretati daemon”.
- Connects existing Sprint 4 shell and daemon foundations into one desktop session object for the eventual app shell bootstrap.
