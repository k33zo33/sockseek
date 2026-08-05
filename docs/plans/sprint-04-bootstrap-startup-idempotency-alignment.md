# Sprint 4 bootstrap startup idempotency alignment

## Goal

Align desktop program bootstrap with idempotent shell-session startup so an already-connected session is treated as a successful startup path.

## Current-state findings

- `DesktopShellSession.StartAsync()` now returns success when the supervisor is already connected.
- `DesktopProgramBootstrap` still short-circuits on `!session.CanStartDaemon` before calling `StartAsync()`.
- That means bootstrap can still fail even though the shell session itself can now start successfully.

## In scope

- Let bootstrap rely on `StartAsync()` as the source of startup success.
- Add focused bootstrap coverage for an already-connected session shape.

## Out of scope

- New daemon discovery mechanisms.
- Avalonia application lifetime code.
- Multi-instance IPC.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-bootstrap-startup-idempotency-alignment.md`

## API, schema and event changes

- No API changes.
- No persistence changes.
- Desktop bootstrap semantics become consistent with shell-session startup behavior.

## Implementation sequence

1. Remove bootstrap's redundant preflight rejection.
2. Extend bootstrap tests.
3. Run desktop tests.

## Testing strategy

- Extend `DesktopProgramBootstrapTests` for an already-connected/no-launch startup path.
- Run `Sockseek.Desktop.Tests` in Docker.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- No secret handling changes.
- No license impact.
- Reduces false-negative startup failures.

## Risks and stop conditions

- Stop if other callers depend on exit code 2 before any `StartAsync()` call.
- Stop if bootstrap needs a richer session state contract first.

## Acceptance-criteria mapping

- Strengthens the automatic daemon startup experience.
- Keeps desktop shell bootstrap and supervisor behavior internally consistent.
