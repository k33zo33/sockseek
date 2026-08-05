# Sprint 4 idempotent shell session start

## Goal

Make desktop shell startup idempotent so the shell session does not launch a second local daemon when the supervisor is already connected.

## Current-state findings

- `DesktopProgramBootstrap` always delegates startup through `IDesktopShellSession.StartAsync()`.
- `DesktopShellSession.StartAsync()` currently launches whenever daemon launching is available.
- If a supervisor/session is already connected, that behavior can attempt an unnecessary second daemon launch.

## In scope

- Treat an already-connected shell session as successfully started.
- Add focused unit coverage for the already-connected case.

## Out of scope

- Broader daemon discovery outside the current supervisor snapshot.
- Multi-process rendezvous changes.
- Avalonia window lifecycle work.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-idempotent-shell-session-start.md`

## API, schema and event changes

- No API changes.
- No schema or persistence changes.
- Desktop startup semantics become safely idempotent for the connected state.

## Implementation sequence

1. Add an early success path for already-connected sessions.
2. Extend shell-session tests.
3. Run desktop tests.

## Testing strategy

- Extend `DesktopShellSessionTests` to verify no new launch occurs when already connected.
- Run `Sockseek.Desktop.Tests` in Docker.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- No new secret handling.
- No license impact.
- Reduces accidental duplicate local-daemon launches.

## Risks and stop conditions

- Stop if restart recovery relies on always forcing a relaunch from the connected state.
- Stop if supervisor snapshots can report connected before a usable handshake exists.

## Acceptance-criteria mapping

- Strengthens the "user does not need to manually start the daemon" experience.
- Improves daemon-supervisor behavior for the development shell deliverable.
