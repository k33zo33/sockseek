# Sprint 15 Docker smoke diagnostics and warning reduction

## Goal

Make Docker/headless smoke failures reproducible and bounded when Docker Desktop or the Docker CLI hangs from the validation session, preserve the evidence for successful Sprint 15 Docker/headless smoke once the local engine is reachable, and avoid relying on unreviewed CLI trimming in the Docker artifact.

## Current-state findings

- `scripts/run_docker_smoke.ps1` bounds most Docker CLI calls with `-DockerCommandTimeoutSeconds`.
- Cleanup now routes `docker logs` and `docker rm -f` through the same bounded Docker invocation path.
- The Sprint 15 go/no-go now records Docker/headless smoke as passed after Docker Desktop storage was moved to `F:\Docker\DockerDesktopWSL`, the Codex sandbox user was added to `docker-users`, Docker Desktop was restarted and the full helper passed end to end on 2026-10-13.
- The helper still emits a structured diagnostic artifact for context, command output and timeout evidence when a future Docker Desktop or Docker CLI run fails.
- Windows Docker Desktop failures can involve either user Docker config access or the Docker Desktop engine pipe; the helper now records a non-fatal isolated-config pipe diagnostic to distinguish those cases.
- The Dockerfile still publishes the CLI as a self-contained single-file binary, but trimming it produced many IL2026/IL2104 warnings through JSON, EF Core, ASP.NET Core and provider dependencies. The daemon publish already disables trimming.

## In scope

- Reuse the bounded Docker invocation path for cleanup commands.
- Add an optional diagnostics Markdown report path for command, output, timeout and environment evidence.
- Add a bounded Windows Docker Desktop pipe diagnostic that does not make the smoke pass or fail by itself.
- Document the diagnostic option in the Docker/headless go/no-go evidence.
- Disable trimming for the Docker CLI publish so the smoke artifact avoids known trim-analysis warnings until trimming is intentionally reviewed.

## Out of scope

- Changing daemon bind policy, provider behavior or application startup topology.
- Reclassifying the existing passed evidence as current if a later validation session cannot reach Docker Desktop.

## Files and projects affected

- `scripts/run_docker_smoke.ps1`
- `Dockerfile`
- `docs/docker.md`
- `docs/beta-go-no-go.md`
- `docs/plans/15-docker-smoke-diagnostics.md`

## API, schema and event changes

No application API, schema or event changes.

## Implementation sequence

1. Add optional `-DiagnosticsPath` support.
2. Record environment context and each Docker command result in the diagnostics report.
3. Route daemon logs and container removal through the bounded Docker helper.
4. Add a non-fatal Windows direct-pipe diagnostic with isolated `DOCKER_CONFIG`.
5. Smoke-test the timeout and access-denied paths with short Docker command timeouts.
6. Disable Docker CLI trimming and rerun the full Docker smoke to confirm the image still builds, CLI help works and daemon `/health` responds.

## Testing strategy

- Run `scripts/run_docker_smoke.ps1 -SkipBuild -SkipComposeConfig -DockerCommandTimeoutSeconds 5 -DockerDesktopPipeDiagnosticTimeoutSeconds 5 -DiagnosticsPath ...` and verify it fails clearly when `docker version` hangs or the process cannot access the Docker Desktop pipe.
- Preserve the successful 2026-10-13 full run evidence in `docs/beta-go-no-go.md`; rerun the full helper before replacing that evidence.
- Run `scripts/test_docker_smoke_diagnostics.ps1` to verify the diagnostic artifact shape without requiring a working Docker engine.
- Run the full `scripts/run_docker_smoke.ps1 -DiagnosticsPath ...` after Dockerfile changes.
- Run `git diff --check`.

## Migration and rollback

No migration. Rollback removes the diagnostics parameter, returns cleanup to the previous direct Docker calls and restores Docker CLI trimming if a future trimmer review proves it safe.

## Security, privacy and license impact

Diagnostics include command names, current Windows user, Docker context output if available and timeout/error text. They must not include provider tokens, OAuth codes, Soulseek passwords or Authorization headers. Disabling Docker CLI trimming increases image size but reduces packaging/runtime risk; it does not change provider audio scope, license, daemon bind policy or data storage.

## Risks and stop conditions

Do not log environment variables wholesale. Do not mark a new Docker/headless smoke complete unless the full helper passes end to end. Stop if disabling trimming breaks single-file CLI execution or daemon startup.

## Acceptance-criteria mapping

- Packaged beta smoke: Docker/headless smoke has passed once for Sprint 15, future blocked runs get bounded failure evidence instead of a hanging validation run, and the Docker CLI artifact no longer emits trim-analysis warnings from unreviewed dependencies during publish.
