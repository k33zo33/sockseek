# Sprint 1 architecture foundation bootstrap

## Goal

Establish the first safe implementation slice for Sprint 1 by introducing the minimum project scaffolding, dependency rules, and validation gates needed to grow the new application layers without changing existing CLI/daemon behavior.

## Current-state findings

- Sprint 0 baseline work is complete and published on `origin/codex/sprint-00-baseline-sync`.
- The solution currently contains only the legacy projects: `Sockseek.Core`, `Sockseek.Api`, `Sockseek.Server`, `Sockseek.Cli`, supporting tools, and existing test projects.
- None of the Sprint 1 skeleton projects exist yet.
- The repo already has `Directory.Build.props` with shared versioning and NuGet lock-file settings, but it does not yet use Central Package Management via `Directory.Packages.props`.
- Sprint 1 scope spans more than three projects and includes new API surface and validation gates, so an explicit execution plan is required before implementation.
- Host `dotnet` is still unavailable in this environment; Docker-hosted .NET SDK validation remains the safe default.

## In scope

- Add the first Sprint 1 execution plan and mark Sprint 1 as the active sprint.
- Create the minimum new solution/project skeletons needed to prove dependency boundaries.
- Introduce Central Package Management for new packages added during Sprint 1.
- Add architecture tests that enforce at least the highest-risk boundary: Desktop must not reference Core directly.
- Add the first `/api/v1/system/info` and `/health` foundation endpoints plus the supporting contract/error envelope only if they fit in the same safe slice.
- Keep legacy CLI and daemon behavior green throughout.

## Out of scope

- Provider-specific integrations.
- SQLite persistence and migrations.
- Real desktop UI behavior beyond a buildable skeleton.
- Soulseek engine refactors or `DownloadEngine` behavior changes.
- Remote-auth, secret-store, or player implementation details beyond basic abstractions/skeletons.

## Files and projects affected

- `docs/project-state.yaml`
- `docs/sprints/README.md`
- `docs/sprints/sprint-01-architecture-foundation.md`
- `docs/plans/sprint-01-architecture-foundation-bootstrap.md`
- `Sockseek.sln`
- `Directory.Packages.props` (new)
- new project folders for Sprint 1 skeletons
- new architecture/system test project(s)
- `Sockseek.Server` and `Sockseek.Server.Tests`

## API, schema and event changes

- Planned new API surface: `/health` and `/api/v1/system/info`
- Planned structured error envelope for new `/api/v1` endpoints
- No persistence schema changes in this bootstrap slice unless a later Sprint 1 step explicitly requires them

## Implementation sequence

1. Mark Sprint 1 as the active sprint and record this bootstrap plan.
2. Add `Directory.Packages.props` and normalize any new package versions through it.
3. Create buildable skeleton projects for Domain, Application, Infrastructure, Integrations.Abstractions, Player, and Desktop.
4. Add the new projects to `Sockseek.sln` with only the allowed references.
5. Add architecture tests for the most important dependency boundaries.
6. Add the first minimal system-info/health endpoints and matching tests if the scaffolding remains small and stable.
7. Run release build + relevant tests in Docker and review the diff for boundary violations and scope creep.

## Testing strategy

- `docker run ... dotnet restore`
- `docker run ... dotnet build -c Release`
- `docker run ... dotnet test -c Release --no-build`
- targeted architecture dependency tests
- targeted server integration/system endpoint tests
- OpenAPI drift/snapshot check if `/api/v1` contracts are introduced in this slice

## Migration and rollback

- No database migration expected in the bootstrap slice.
- Rollback is standard git revert of the Sprint 1 bootstrap commits.
- Keep changes layered so project scaffolding can be reverted without touching Sprint 0 baseline artifacts.

## Security, privacy and license impact

- Preserve AGPL artifacts and existing release-checklist requirements.
- Keep new API endpoints loopback/local-host oriented and free of secret leakage.
- Do not expose provider tokens, Soulseek credentials, or mutable Core internals through the new layers.

## Risks and stop conditions

- Stop if the required project skeletons force a locked ADR change.
- Stop if Desktop scaffolding requires provider SDKs or direct `Sockseek.Core` references.
- Stop if adding `/api/v1` endpoints breaks existing OpenAPI/legacy API expectations without a clean compatibility path.
- Stop if Docker-based validation becomes insufficient to verify the slice.

## Acceptance-criteria mapping

- New layer skeletons: covered by planned solution/project scaffolding steps.
- Central Package Management: covered by `Directory.Packages.props` introduction.
- Architecture tests: covered by dedicated dependency-boundary tests.
- System endpoints: covered by minimal `/health` and `/api/v1/system/info` slice if added.
- Legacy API/CLI still green: covered by full Docker build/test validation.
