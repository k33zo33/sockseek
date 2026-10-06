# Performance report

Sprint 15 uses deterministic opt-in tests and benchmarks for large-data evidence. Default `dotnet test` remains fast; heavy fixtures run only when explicitly enabled.

## Current budgets

| Area | Fixture | Budget | Gate |
| --- | --- | --- | --- |
| Local library search | 100,000 canonical tracks with one available media file each | First page under 5 seconds | `LargeDataPerformanceTests.SearchAsync_HundredThousandTrackFixture_ReturnsFirstPageWithinBudget` |
| Playlist detail projection | 10,000 imported playlist items | Detail projection under 10 seconds | `LargeDataPerformanceTests.GetDetailAsync_TenThousandItemPlaylist_ReturnsDetailWithinBudget` |
| Soulseek result display mapping | 10,000 file candidates through the Desktop API client | Result refresh under 2 seconds | `DesktopSearchViewModelTests.RefreshResultsAsync_TrackMode_MapsTenThousandSoulseekResultsWithinBudget` |
| Desktop playlist filtering | 10,000 playlist item view models | Search or status filter refresh under 1 second | `DesktopPlaylistsViewModelTests.SelectedPlaylistItems_FiltersTenThousandItemsWithinBudget` |

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

Eight-hour memory soak remains opt-in and is intentionally excluded from the default suite:

```powershell
$env:SOCKSEEK_RUN_SOAK='1'
$env:SOCKSEEK_SOAK_MINUTES='480'
$env:SOCKSEEK_SOAK_ITEMS_PER_CYCLE='100'
$env:SOCKSEEK_SOAK_CYCLE_DELAY_MS='30000'
dotnet test Sockseek.Server.Tests\Sockseek.Server.Tests.csproj `
  -c Release `
  --no-build `
  --filter SoakStabilityTests `
  --logger "console;verbosity=detailed"
Remove-Item Env:\SOCKSEEK_RUN_SOAK
Remove-Item Env:\SOCKSEEK_SOAK_MINUTES
Remove-Item Env:\SOCKSEEK_SOAK_ITEMS_PER_CYCLE
Remove-Item Env:\SOCKSEEK_SOAK_CYCLE_DELAY_MS
```

## Latest local results

Recorded on 2026-10-06:

- `dotnet test Sockseek.Infrastructure.Tests\Sockseek.Infrastructure.Tests.csproj -c Release --no-build --filter LargeDataPerformanceTests --logger "console;verbosity=detailed"` with `SOCKSEEK_RUN_LARGE_DATA=1` passed.
- 100k library search elapsed query time: `00:00:00.5956801`.
- 10k playlist detail elapsed query time: `00:00:00.2506731`.
- Total test process time, including fixture generation: `1.1649 Minutes`.
- `dotnet test Sockseek.Desktop.Tests\Sockseek.Desktop.Tests.csproj -c Release --no-build --filter DesktopSearchViewModelTests --logger "console;verbosity=detailed"` passed; the 10k Soulseek result display mapping test completed in `151 ms`.
- `dotnet test Sockseek.Desktop.Tests\Sockseek.Desktop.Tests.csproj -c Release --no-build --filter DesktopPlaylistsViewModelTests --logger "console;verbosity=detailed"` passed; the 10k playlist filtering test completed in `130 ms`.
- `dotnet test Sockseek.Server.Tests\Sockseek.Server.Tests.csproj -c Release --no-build --filter EventTrafficProfilingTests --logger "console;verbosity=detailed"` with `SOCKSEEK_RUN_EVENT_PROFILE=1` passed all three event traffic profile gates in `43.4680 Seconds`.
- Large workflow cancellation event traffic stayed within budget: `106` network messages, `164.1 KiB` serialized payload, `101` cancel-delta messages and `59.0 KiB` cancel-delta payload.
- Large aggregate completion with matching results stayed within budget: `2` network messages and `167.2 KiB` serialized payload.
- Large no-result aggregate completion stayed within budget: `112` network messages and `7.71 MiB` serialized payload.
- `dotnet test Sockseek.Server.Tests\Sockseek.Server.Tests.csproj -c Release --no-build --filter SoakStabilityTests --logger "console;verbosity=detailed"` passed as a default no-op when `SOCKSEEK_RUN_SOAK` was unset.
- A one-minute soak harness smoke with `SOCKSEEK_RUN_SOAK=1`, `SOCKSEEK_SOAK_MINUTES=1` and `SOCKSEEK_SOAK_ITEMS_PER_CYCLE=10` passed. It completed 1,016 unthrottled cycles with peak managed heap growth `57.20 MiB` and peak private memory growth `120.26 MiB`; this validates the harness only and does not satisfy the eight-hour acceptance gate.

## Notes

- These tests use in-memory SQLite and measure query/projection paths after fixture creation.
- The one-minute soak smoke does not prove an eight-hour soak run or UI memory stability; those remain separate Sprint 15 gates.
- The current Desktop library, search candidate and playlist item lists have bounded `ListBox` surfaces, but the rendered trace in `docs/desktop-virtualization-trace.md` still needs capture before beta go/no-go.
