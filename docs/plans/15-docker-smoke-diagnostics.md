# Sprint 15 Docker smoke diagnostics

## Goal

Make Docker/headless smoke failures reproducible and bounded when Docker Desktop or the Docker CLI hangs from the validation session.

## Current-state findings

- `scripts/run_docker_smoke.ps1` bounds most Docker CLI calls with `-DockerCommandTimeoutSeconds`.
- The cleanup block still calls `docker logs` and `docker rm -f` directly, so cleanup can hang if the Docker engine is unresponsive.
- The Sprint 15 go/no-go currently records Docker/headless smoke as environment-blocked, but the helper does not emit a structured diagnostic artifact for that failure.

## In scope

- Reuse the bounded Docker invocation path for cleanup commands.
- Add an optional diagnostics Markdown report path for command, output, timeout and environment evidence.
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
4. Smoke-test the timeout path with a short Docker command timeout.

## Testing strategy

- Run `scripts/run_docker_smoke.ps1 -SkipBuild -SkipComposeConfig -DockerCommandTimeoutSeconds 5 -DiagnosticsPath ...` and verify it fails clearly when `docker version` hangs.
- Run `git diff --check`.

## Migration and rollback

No migration. Rollback removes the diagnostics parameter and returns cleanup to the previous direct Docker calls.

## Security, privacy and license impact

Diagnostics include command names, current Windows user, Docker context output if available and timeout/error text. They must not include provider tokens, OAuth codes, Soulseek passwords or Authorization headers.

## Risks and stop conditions

Do not log environment variables wholesale. Do not mark Docker/headless smoke complete unless the full helper passes end to end.

## Acceptance-criteria mapping

- Packaged beta smoke: blocked Docker/headless smoke gets bounded failure evidence instead of a hanging validation run.
