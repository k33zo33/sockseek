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
```

Results:

- restore: passed
- release build: passed
- tests: passed
  - `Sockseek.Server.Tests`: 79 passed
  - `Sockseek.Core.Tests`: 578 passed
  - `Sockseek.Cli.Tests`: 254 passed
- manual legacy CLI help check: passed

## Known warnings / issues

- NuGet vulnerability warning: `AngleSharp` `1.4.0` reports advisory `GHSA-pgww-w46g-26qg` during restore/build.
- `docs/api.md` (upstream) and `docs/API.md` (Codex package) coexist; this is valid on Linux but may be awkward on case-insensitive filesystems/tooling.
- Current `Dockerfile` still installs `dotnet6-sdk` and `dotnet6-runtime` even though the repo targets `net10.0`; Docker packaging is not yet a validated release path.
- Read-only helper wrapper is present, but local helper authentication currently needs attention:
  - `gemini` CLI reports unsupported/ineligible client tier in this environment
  - `claude` CLI reports expired OAuth authentication in this environment

## Sprint 0 follow-up items

- add/update remaining Sprint 0 documentation artifacts and release/legal checklist items
- decide how to normalize or live with `docs/api.md` vs `docs/API.md`
- refresh Docker packaging for `net10.0`
- refresh helper authentication if Gemini/Claude reviews are expected in regular workflow
