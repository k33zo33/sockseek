# Sprint 04 dynamic desktop daemon binding

## Goal

Make the Avalonia desktop development daemon launch on a conflict-free loopback port so sprint-04 daemon startup and reconnect integration tests pass reliably even when the default daemon port is already occupied.

## Current-state findings

- `DesktopDevelopmentDaemonLaunchRequestFactory` launches `Sockseek.Server` with `dotnet run --project Sockseek.Server/Sockseek.Server.csproj --no-launch-profile` and no explicit URL override.
- `ServerHost.Build` currently calls `UseUrls(ResolveListenUrl(url))`, and `ResolveListenUrl(null)` hard-defaults to `http://127.0.0.1:5030`.
- Desktop integration tests fail when port `5030` is already in use, so the daemon never emits a handshake and `DesktopDaemonSupervisor.TryLaunchAsync` returns `false`.
- A desktop shell handshake already reports the actual bound address, so using an ephemeral loopback port fits the existing handshake model.

## In scope

- Allow server startup to honor an explicit command-line or environment URL before falling back to the fixed default.
- Make desktop development launches request an ephemeral loopback URL.
- Update desktop/server tests to cover the new binding behavior and keep existing default-port behavior for non-desktop callers.
- Re-run the relevant desktop and server validation suites.

## Out of scope

- Production packaging changes.
- Remote/public binding behavior beyond current loopback defaults.
- Broader daemon process supervision or API redesign.
- Addressing unrelated NuGet vulnerability warnings.

## Files and projects affected

- `Sockseek.Server`
- `Sockseek.Server.Tests`
- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- plan doc under `docs/plans/`

## API, schema and event changes

- No HTTP contract changes.
- No schema or SignalR event changes.
- Startup binding precedence changes only for process launch configuration.

## Implementation sequence

1. Update server listen URL resolution so an explicit launch-provided URL wins over the default.
2. Update desktop development daemon launch requests to ask for `http://127.0.0.1:0`.
3. Adjust unit/integration tests for the new launch request and binding precedence.
4. Run targeted server/desktop tests, then the sprint-required desktop test suite.

## Testing strategy

- `dotnet test Sockseek.Server.Tests/Sockseek.Server.Tests.csproj -c Release`
- `dotnet test Sockseek.Desktop.Tests/Sockseek.Desktop.Tests.csproj -c Release`
- If needed, rerun failing desktop integration tests with normal console verbosity for diagnostics.

## Migration and rollback

- No data migration.
- Rollback is straightforward by reverting the binding-precedence and launch-request changes.

## Security, privacy and license impact

- Keep daemon binding on loopback only.
- Session-token handshake remains required for protected `/api/v1` endpoints.
- No license impact.

## Risks and stop conditions

- Stop if honoring command-line/environment URLs would weaken the loopback-only desktop security assumption.
- Stop if Kestrel/host address reporting does not reliably emit the actual ephemeral port for the desktop handshake.
- Watch for brittle tests that assume a fixed `5030` handshake URL.

## Acceptance-criteria mapping

- User does not need to manually start the daemon: improved by making development launch resilient to port conflicts.
- UI recovers after controlled daemon restart: preserved by handshake-driven reconnect on a fresh port/token.
- Desktop avoids `Sockseek.Core`/`DbContext` references: unchanged.
- Main pages/navigation/theme persistence: unaffected by this slice.
