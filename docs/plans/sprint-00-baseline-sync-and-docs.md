# Sprint 0 baseline sync and documentation setup

## Goal

Establish a reproducible Sprint 0 baseline for the `k33zo33/sockseek` fork, sync it to reviewed upstream, add the Codex planning/docs package in repo-native locations, and close the missing baseline documentation gaps required by Sprint 0.

## Current-state findings

- The fork started at documented baseline commit `ef36306c86046757d76d6c1158a48c7b2f58dc2c`.
- `origin` points to `k33zo33/sockseek`.
- `upstream/master` is ahead of the baseline and fast-forwardable.
- Local host runtime does not expose `dotnet`, but Docker is available.
- The Codex documentation package was initially nested under `docs/Sockseek_Codex_Documentation_Package/` and needed normalization.
- The repo currently has both `docs/api.md` and `docs/API.md`, which may be awkward across case-insensitive tooling.

## In scope

- add upstream remote and baseline tag
- fast-forward a Sprint 0 branch to upstream/master
- normalize the Codex documentation package into root/docs/reference
- add missing Sprint 0 documents such as product scope and provider capability matrix
- update README and baseline-facing docs as needed for locked product decisions
- run Sprint 0 validation via Docker-hosted .NET SDK if direct host SDK is unavailable
- add lightweight read-only Gemini/Claude helper workflow tooling requested by the user

## Out of scope

- Avalonia/Desktop implementation
- database or API redesign beyond Sprint 0 docs/baseline work
- changing locked ADR decisions
- provider playback/download capabilities

## Files and projects affected

- repository root docs and helper scripts
- `README.md`
- `docs/*`
- `tool/ai_helper.sh`

## API, schema and event changes

- none intended for Sprint 0 documentation/setup work

## Implementation sequence

1. confirm baseline, remotes, tag, and clean branch strategy
2. fast-forward Sprint 0 branch to upstream/master
3. replay documentation import onto the Sprint 0 branch
4. add missing Sprint 0 docs and helper tooling
5. validate restore/build/test using Docker-hosted .NET SDK
6. review resulting diff for scope, security, and compatibility

## Testing strategy

- `docker run ... dotnet restore`
- `docker run ... dotnet build -c Release`
- `docker run ... dotnet test -c Release --no-build`
- manual `sockseek --help` check if a built artifact is available within the Docker validation flow

## Migration and rollback

- no persistence migration expected
- rollback is standard git revert/cherry-pick cleanup on the Sprint 0 branch

## Security, privacy and license impact

- preserve AGPL licensing artifacts and source-link requirements
- helper tooling remains read-only and does not store secrets
- no provider or Soulseek credential handling changes in this slice

## Risks and stop conditions

- if upstream no longer fast-forwarded cleanly, stop and document divergence
- if Docker-based validation fails for environmental reasons, capture the blocker precisely
- if locked product decisions need to change, stop and require an ADR

## Acceptance-criteria mapping

- baseline synced on a non-master working branch
- documented repo contains required ADRs and Sprint 0 docs additions
- provider playback/download remains explicitly forbidden in docs
- validation attempted with concrete results recorded
