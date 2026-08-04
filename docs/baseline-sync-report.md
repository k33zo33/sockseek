# Sprint 0 baseline sync report

## Baseline

- documented fork baseline: `ef36306c86046757d76d6c1158a48c7b2f58dc2c`
- baseline tag: `sockseek-ui-baseline-ef36306`
- upstream remote: `https://github.com/fiso64/sockseek.git`
- synchronized upstream head used for Sprint 0 branch: `7bcc1909e24083453716fda38a23e6663c0b78d6`

## Branch strategy

- `master` preserved at the documented baseline commit
- `codex/sprint-00-baseline-sync` fast-forwarded to `upstream/master`
- Codex planning/docs package replayed on top of the Sprint 0 branch

## Validation

Host `dotnet` was unavailable, so validation ran in Docker using `mcr.microsoft.com/dotnet/sdk:10.0`.

Commands:

```bash
docker run --rm -v /home/server/repo/sockseek:/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 \
  bash -lc 'dotnet restore && dotnet build -c Release && dotnet test -c Release --no-build'

docker run --rm -v /home/server/repo/sockseek:/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 \
  bash -lc 'dotnet run --project Sockseek.Cli -c Release -- --help'

docker build -t sockseek:sprint0-net10 .

docker run --rm --entrypoint /usr/bin/sockseek sockseek:sprint0-net10 --help

docker compose config
```

Results:

- restore: passed
- release build: passed
- tests: passed
  - `Sockseek.Server.Tests`: 79 passed
  - `Sockseek.Core.Tests`: 578 passed
  - `Sockseek.Cli.Tests`: 254 passed
- manual legacy CLI help check: passed
- Docker image build (`sockseek:sprint0-net10`): passed after updating the Dockerfile to `net10.0` and suppressing publish-time OpenAPI generation for RID/self-contained trimmed publishes
- packaged container CLI help (`/usr/bin/sockseek --help`): passed
- `docker compose config`: passed
- live compose review: container starts cron-capable init stack successfully, but does not auto-start `sockseek daemon`

Revalidation on current `codex/sprint-00-baseline-sync` HEAD repeated the same required gates successfully:

- `docker run ... dotnet restore && dotnet build -c Release && dotnet test -c Release --no-build`: passed
- `docker run ... dotnet run --project Sockseek.Cli -c Release -- --help`: passed
- `docker build -t sockseek:sprint0-net10 .`: passed
- `docker run --rm --entrypoint /usr/bin/sockseek sockseek:sprint0-net10 --help`: passed
- `docker compose config`: passed

## Known warnings / issues

- `THIRD-PARTY-NOTICES` now exists as a tracked baseline artifact, but public releases still need a release-specific review of the exact resolved dependency graph and bundled notice texts.
- NuGet vulnerability warning: `AngleSharp` `1.4.0` reports advisory `GHSA-pgww-w46g-26qg` during restore/build.
- Docker `dotnet publish` for the trimmed self-contained CLI image still emits linker/trim-analysis warnings (for example ASP.NET Core MVC/SignalR reflection paths, JSON serialization, `EmbedIO`, `Soulseek`, `SpotifyAPI.Web`, and related dependencies). The image builds and the packaged CLI help command works, but public packaging should treat these as review items rather than silently assuming trim safety.
- `docs/api.md` (current daemon/client integration) and `docs/API.md` (planned application API) intentionally coexist; this is valid on Linux but may still be awkward on case-insensitive filesystems/tooling.
- Docker remains a secondary headless packaging path; it is not the primary desktop distribution mechanism.
- Live compose review confirmed that the default container starts cron support but does not auto-start `sockseek daemon` or expose the daemon API port `5030`; the published `127.0.0.1:48721` port is for provider login callbacks such as Spotify PKCE.
- Read-only helper wrapper is present, but local helper authentication currently needs attention:
  - `gemini` CLI reports unsupported/ineligible client tier in this environment
  - `claude` CLI reports expired OAuth authentication in this environment

## Sprint 0 follow-up items

- expand `THIRD-PARTY-NOTICES` from baseline inventory into a release-specific bundled notices artifact when public packaging begins
- keep `docs/release-checklist.md`, `docs/INDEX.md`, baseline/specification docs, and daemon-facing README guidance aligned with the concrete repo artifacts and safe default release posture
- keep the current `docs/api.md` vs `docs/API.md` split documented until a future doc reorganization removes the case-sensitive filename distinction
- decide whether a future daemon-first compose profile should be added, or whether Docker remains explicitly CLI/cron-oriented
- refresh helper authentication if Gemini/Claude reviews are expected in regular workflow, while keeping helper use optional rather than a hard gate for local Sprint 0 validation
