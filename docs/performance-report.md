# Performance report

Sprint 15 uses deterministic opt-in tests and benchmarks for large-data evidence. Default `dotnet test` remains fast; heavy fixtures run only when explicitly enabled.

## Current budgets

| Area | Fixture | Budget | Gate |
| --- | --- | --- | --- |
| Local library search | 100,000 canonical tracks with one available media file each | First page under 5 seconds | `LargeDataPerformanceTests.SearchAsync_HundredThousandTrackFixture_ReturnsFirstPageWithinBudget` |
| Playlist detail projection | 10,000 imported playlist items | Detail projection under 10 seconds | `LargeDataPerformanceTests.GetDetailAsync_TenThousandItemPlaylist_ReturnsDetailWithinBudget` |

## How to run

```powershell
$env:SOCKSEEK_RUN_LARGE_DATA='1'
dotnet test Sockseek.Infrastructure.Tests\Sockseek.Infrastructure.Tests.csproj `
  -c Release `
  --no-build `
  --filter LargeDataPerformanceTests `
  --logger "console;verbosity=detailed"
Remove-Item Env:\SOCKSEEK_RUN_LARGE_DATA
```

Event traffic profiling remains separately gated:

```powershell
$env:SOCKSEEK_RUN_EVENT_PROFILE='1'
dotnet test Sockseek.Server.Tests\Sockseek.Server.Tests.csproj `
  -c Release `
  --no-build `
  --filter EventTrafficProfilingTests `
  --logger "console;verbosity=detailed"
Remove-Item Env:\SOCKSEEK_RUN_EVENT_PROFILE
```

## Latest local results

Recorded on 2026-10-06:

- `dotnet test Sockseek.Infrastructure.Tests\Sockseek.Infrastructure.Tests.csproj -c Release --no-build --filter LargeDataPerformanceTests --logger "console;verbosity=detailed"` with `SOCKSEEK_RUN_LARGE_DATA=1` passed.
- 100k library search elapsed query time: `00:00:00.5956801`.
- 10k playlist detail elapsed query time: `00:00:00.2506731`.
- Total test process time, including fixture generation: `1.1649 Minutes`.

## Notes

- These tests use in-memory SQLite and measure query/projection paths after fixture creation.
- They do not prove an eight-hour soak run or UI memory stability; those remain separate Sprint 15 gates.
- The current Desktop library list has a bounded list surface, but a rendered virtualized performance trace still needs capture before beta go/no-go.
