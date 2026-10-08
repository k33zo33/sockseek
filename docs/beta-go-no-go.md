# Sprint 15 beta go/no-go

Recorded on 2026-10-06.

## Decision

| Release type | Decision | Reason |
| --- | --- | --- |
| Public beta | NO-GO | ADR-0009 blocks public beta until Soulseek compliance coverage is resolved. The eight-hour soak and rendered UI virtualization trace are not yet captured. |
| Closed/internal beta | CONDITIONAL GO | Closed testing can continue for packaging, provider metadata import, local library, local/progressive playback, diagnostics and performance validation when testers receive the beta limitations notice. |
| Stable release | NO-GO | Stable release still needs public beta gates, code signing/rollback decisions and broader packaging evidence. |

## Evidence

| Gate | Current evidence | Status |
| --- | --- | --- |
| Soulseek compliance decision | `docs/soulseek-compliance-audit.md` and `docs/adr/0009-closed-beta-until-soulseek-compliance.md` document the closed-beta-only decision. | Public beta blocked |
| 100k library search | `LargeDataPerformanceTests.SearchAsync_HundredThousandTrackFixture_ReturnsFirstPageWithinBudget` passed opt-in large-data run at `00:00:00.5956801`. | Passed |
| 10k playlist detail projection | `LargeDataPerformanceTests.GetDetailAsync_TenThousandItemPlaylist_ReturnsDetailWithinBudget` passed opt-in large-data run at `00:00:00.2506731`. | Passed |
| Large Soulseek result display | `DesktopSearchViewModelTests.RefreshResultsAsync_TrackMode_MapsTenThousandSoulseekResultsWithinBudget` passed in `151 ms`. | Passed |
| Desktop playlist filtering | `DesktopPlaylistsViewModelTests.SelectedPlaylistItems_FiltersTenThousandItemsWithinBudget` passed in `130 ms`. | Passed |
| Event traffic profiling | `EventTrafficProfilingTests` passed the cancellation, matching-result completion and no-result completion profile gates. The largest recorded profile was `112` network messages and `7.71 MiB` serialized payload for the 3,000-job no-result aggregate completion path. | Passed |
| Provider recovery UX | `docs/provider-failure-matrix.md` documents recovery paths; provider endpoint tests cover rate-limit recovery and token/error surfaces. | Passed for covered providers |
| Release limitations | `docs/beta-limitations.md` states Spotify quota limits, Bandcamp/MusicBrainz scope and provider no-audio policy. | Passed |
| Provider-audio scope guard | `scripts/run_provider_audio_guard.ps1` passed on 2026-10-08, scanning 428 production source files and 278 provider-boundary files for forbidden provider playback/download/audio markers while preserving legacy Core/CLI compatibility boundaries. | Passed |
| Security and crash reporting | Root `SECURITY.md`, `docs/diagnostics-feedback.md` and GitHub issue templates route private vulnerabilities, crash reports and redacted diagnostics. | Passed |
| Dependency vulnerability scan | `dotnet list package --vulnerable --include-transitive` passed on 2026-10-08 for commit `2a6b53a` using `https://api.nuget.org/v3/index.json`; no project reported known vulnerable packages. | Passed |
| Full solution validation | `dotnet build -c Release` passed on 2026-10-08 after cleaning Desktop test fake-event warnings with `0 Warning(s), 0 Error(s)`. `dotnet test -c Release --no-build` passed on commit `72450b0` across all test projects: Architecture 5, Application 55, Domain 26, Packager 13, Infrastructure 74, Core 578, Player 34, CLI 258, Desktop 229 and Server 159 tests. Release build regenerated `docs/openapi.json` with no Git drift, and `scripts/run_provider_audio_guard.ps1` passed. | Passed |
| Windows package smoke | `scripts/run_windows_package_smoke.ps1 -Force` passed for commit `08967e1`: Windows Desktop and daemon publish, legal files, release metadata, 126-package SBOM, staging validation, SHA256 manifest, Desktop and daemon executables and native dependencies. Local publish emitted `NU1900` warnings because NuGet vulnerability data was unavailable. | Passed for Windows RC |
| Soak stability | `SoakStabilityTests.RepeatedWorkflowSoak_DoesNotExceedMemoryGrowthBudget` exists, `scripts/run_sprint15_soak.ps1` standardizes the opt-in run, JSON report artifact and transcript log artifact, completed in-memory workflow history is capped, and one-minute harness/helper smokes passed. The latest helper smoke on 2026-10-08 completed 1,039 unthrottled cycles with managed growth `45.77 MiB` and private growth `95.78 MiB`. The required eight-hour run is not captured. | Incomplete |
| Rendered UI virtualization trace | `docs/desktop-virtualization-trace.md` defines the trace checklist and `scripts/capture_desktop_virtualization_trace.ps1` standardizes process metric capture. The helper now requires a rendered main window by default and its `-AllowHeadlessProcess` harness smoke passed. XAML guards prove bounded `ListBox` surfaces with explicit `VirtualizingStackPanel` panels for large library/search/playlist lists, but the required rendered Desktop traces are not captured. | Incomplete |
| Docker/headless smoke | `scripts/run_docker_smoke.ps1` standardizes compose validation, image build, CLI help and daemon `/health` checks. Adding `.dockerignore` reduced build context from more than `3.35 GiB` to about `66 KiB`; `Dockerfile` now restores `Sockseek.HelpGenerator` and installs `vlc-libs` for daemon startup. The latest local rerun found Docker Desktop UI/backend processes running, but `docker version`/`docker info` hung, WSL initially reported `docker-desktop` as stopped, and `com.docker.service` could not be started from this session because Windows returned `Access is denied`. Full build/run validation remains incomplete until the Docker engine responds and the helper passes end to end. | Environment-blocked |

## Required before public beta

1. Resolve ADR-0009 by either implementing the missing Soulseek compliance work or accepting a superseding decision.
2. Run the eight-hour soak command in `docs/performance-report.md` and record the memory growth results here.
3. Capture the rendered Desktop UI virtualization trace described in `docs/desktop-virtualization-trace.md`.
4. Complete target OS package smoke for every public beta artifact.
5. Update release notes with the exact commit/tag, beta limitations, legal-use notice and source availability.

## Closed beta requirements

- Distribute only to internal or allowlisted testers.
- Include `docs/beta-limitations.md`, root `SECURITY.md`, `LICENSE` and `THIRD-PARTY-NOTICES`.
- Identify the exact commit in tester instructions.
- Ask testers to use the `Copy diagnostics` action when it is available and never paste tokens, OAuth codes, client secrets, Soulseek passwords or full Authorization headers.
- Do not present the build as a public beta or stable release.
