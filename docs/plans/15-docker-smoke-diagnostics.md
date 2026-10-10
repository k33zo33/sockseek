# Sprint 15 Docker smoke diagnostics

## Goal

Make Docker/headless smoke failures reproducible and bounded when Docker Desktop or the Docker CLI hangs from the validation session.

## Current-state findings

- `scripts/run_docker_smoke.ps1` bounds most Docker CLI calls with `-DockerCommandTimeoutSeconds`.
- Cleanup now routes `docker logs` and `docker rm -f` through the same bounded Docker invocation path.
- The Sprint 15 go/no-go records Docker/headless smoke as environment-blocked, and the helper emits a structured diagnostic artifact for context, command output and timeout evidence.
- Windows Docker Desktop failures can involve either user Docker config access or the Docker Desktop engine pipe; the helper now records a non-fatal isolated-config pipe diagnostic to distinguish those cases.

## In scope

- Reuse the bounded Docker invocation path for cleanup commands.
- Add an optional diagnostics Markdown report path for command, output, timeout and environment evidence.
- Add a bounded Windows Docker Desktop pipe diagnostic that does not make the smoke pass or fail by itself.
- Document the diagnostic option in the Docker/headless go/no-go evidence.

## Out of scope

- Changing Dockerfile runtime behavior, daemon bind policy, provider behavior or application startup topology.
- Claiming Docker/headless smoke is passed while the local Docker engine still hangs.

## Files and projects affected

- `scripts/run_docker_smoke.ps1`
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

## Testing strategy

- Run `scripts/run_docker_smoke.ps1 -SkipBuild -SkipComposeConfig -DockerCommandTimeoutSeconds 5 -DockerDesktopPipeDiagnosticTimeoutSeconds 5 -DiagnosticsPath ...` and verify it fails clearly when `docker version` hangs or the process cannot access the Docker Desktop pipe.
- Run `scripts/test_docker_smoke_diagnostics.ps1` to verify the diagnostic artifact shape without requiring a working Docker engine.
- Run `git diff --check`.

## Migration and rollback

No migration. Rollback removes the diagnostics parameter and returns cleanup to the previous direct Docker calls.

## Security, privacy and license impact

Diagnostics include command names, current Windows user, Docker context output if available and timeout/error text. They must not include provider tokens, OAuth codes, Soulseek passwords or Authorization headers.

## Risks and stop conditions

Do not log environment variables wholesale. Do not mark Docker/headless smoke complete unless the full helper passes end to end.

## Acceptance-criteria mapping

- Packaged beta smoke: blocked Docker/headless smoke gets bounded failure evidence instead of a hanging validation run.
