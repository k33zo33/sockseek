# Sprint 2 Soulseek gateway and local API security foundation

## Goal

Establish the safest first execution slice for Sprint 2 by closing Sprint 1, then introducing a plan for the new engine gateway boundary and local API protection without breaking the legacy CLI or daemon API.

## Current-state findings

- Sprint 1 acceptance work appears complete: new layer skeletons exist, architecture tests are green, system endpoints exist, structured `/api/v1` errors + correlation IDs exist, and full Docker build/test is passing.
- Sprint 2 spans multiple projects (`Sockseek.Application`, `Sockseek.Server`, `Sockseek.Api`, tests, and possibly `Sockseek.Desktop` for client/auth consumption), so an explicit plan is required before implementation.
- The repository already exposes the first `/api/v1/system/*` endpoints but does not yet enforce local session-token authentication.
- The server currently preserves legacy routes and behavior; Sprint 2 must keep that compatibility intact.
- Host `dotnet` is unavailable here, so Docker-hosted .NET SDK remains the safe validation path.

## In scope

- Mark Sprint 1 complete and promote Sprint 2 to active.
- Define the minimum Sprint 2 implementation order for gateway and local API security.
- Identify the first safe code slice: session-token primitives and `/api/v1` auth middleware around the already introduced versioned API surface.
- Preserve anonymous limited health access and existing legacy API compatibility.

## Out of scope

- Full provider/account flows.
- Desktop UI onboarding behavior.
- SQLite persistence or secret-store implementation details beyond interfaces/placeholders needed for auth flow.
- Broad engine refactors outside the adapter boundary.

## Files and projects likely affected

- `docs/project-state.yaml`
- `docs/sprints/README.md`
- `docs/sprints/sprint-01-architecture-foundation.md`
- `docs/sprints/sprint-02-soulseek-gateway-local-api-security.md`
- `docs/plans/sprint-02-gateway-auth-foundation.md`
- `Sockseek.Api`
- `Sockseek.Server`
- `Sockseek.Application` (if gateway contracts land in the first code slice)
- `Sockseek.Server.Tests`
- `Sockseek.Cli.Tests` and/or new parity/auth-focused tests

## Planned first code slice

1. Add a minimal session-token provider/options model inside the server host boundary.
2. Add middleware that enforces the token only for `/api/v1` routes that are meant to be protected, while keeping `/health` and `/api/v1/system/health` anonymous/minimal.
3. Return a stable 401 envelope for unauthorized `/api/v1` requests without changing legacy endpoint behavior.
4. Add integration tests for authorized/unauthorized access.
5. Validate full solution compatibility in Docker before moving on to gateway contracts.

## Follow-up slices after auth foundation

1. Introduce `ISoulseekEngineGateway` contract and immutable snapshot/event models.
2. Map track search, album search, download, cancel, and next-candidate through the adapter boundary.
3. Add parity tests comparing gateway-backed flows with legacy backend fixtures.
4. Add correlation/workflow mapping and progress event coalescing.

## Testing strategy

- `docker run ... dotnet restore`
- `docker run ... dotnet build -c Release`
- `docker run ... dotnet test -c Release --no-build`
- targeted auth middleware integration tests
- targeted parity tests once gateway contracts land
- preserve existing architecture, CLI, and server contract coverage

## Security, privacy and license impact

- Moves the repo toward the documented loopback + session-token local API model.
- Must not log or echo session tokens or Authorization headers.
- Keeps AGPL and local-first constraints unchanged.

## Risks and stop conditions

- Stop if protecting `/api/v1` breaks documented anonymous health behavior.
- Stop if the first gateway contract requires direct exposure of mutable `Sockseek.Core` job objects.
- Stop if local auth enforcement would break legacy CLI/API compatibility without a clean boundary.

## Acceptance mapping for this planning slice

- Sprint 1 completion is recorded in project docs.
- Sprint 2 is activated with a concrete, minimal implementation sequence.
- The first implementation target is constrained to auth foundation before broader adapter work.
