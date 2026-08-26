# Sprint 4 program startup bootstrap

## Goal

Connect the desktop entrypoint to the existing shell session so the executable can auto-launch the development daemon and support a testable startup mode.

## Current-state findings

- `DesktopProgramRunner` currently only enforces single-instance gating.
- `DesktopShellSession` now composes the supervisor, recovery coordinator, and shell navigation, but `Program` does not create or start it.
- `Program.Main` still exits immediately, so the current desktop executable does not yet exercise the Sprint 4 shell session path.

## In scope

- Add startup option parsing for a minimal development-shell bootstrap.
- Add a testable bootstrap component that uses `DesktopProgramRunner` and `DesktopShellSession`.
- Update `Program.Main` to create a real shell session with the system process launcher.
- Support an `--exit-after-startup` mode for headless smoke usage and tests.
- Add focused unit tests for startup success, startup failure, and option parsing.

## Out of scope

- Full Avalonia `App` and window bootstrap.
- Installer/runtime packaging behavior.
- Rich command-line UX beyond the minimal development-shell flags needed here.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-program-startup-bootstrap.md`

## API, schema and event changes

- No server API changes.
- No schema or migration changes.
- Desktop-only startup composition changes.

## Implementation sequence

1. Add minimal program options parsing.
2. Add a bootstrap component that creates/starts a shell session through the single-instance runner.
3. Update `Program` to use the bootstrap with a real system launcher.
4. Add unit tests and run desktop tests.

## Testing strategy

- Add focused bootstrap tests using fake shell sessions.
- Run the desktop test project in Docker.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- Reuses the existing loopback-only daemon launch and handshake path.
- No new secret handling.
- No license impact.

## Risks and stop conditions

- Stop if useful bootstrap behavior requires real Avalonia application lifecycle code.
- Stop if default launch behavior needs packaging/runtime decisions outside current Sprint 4 development-shell scope.

## Acceptance-criteria mapping

- Strengthens “Korisnik ne mora ručno pokretati daemon”.
- Moves the Sprint 4 development shell from loose components toward a real executable startup flow.
