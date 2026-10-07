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
| Security reporting | Root `SECURITY.md` and GitHub issue templates route private vulnerabilities and redacted diagnostics. | Passed |
| Windows package smoke | Sprint 15 package smoke for commit `f157c5b` verified Windows Desktop and daemon publish, legal files, release metadata, 126-package SBOM, staging validation, SHA256 manifest, Desktop and daemon executables and native dependencies. Local publish emitted `NU1900` warnings because NuGet vulnerability data was unavailable. | Passed for Windows RC |
| Soak stability | `SoakStabilityTests.RepeatedWorkflowSoak_DoesNotExceedMemoryGrowthBudget` exists, supports a JSON report artifact through `SOCKSEEK_SOAK_REPORT_PATH`, caps completed in-memory workflow history, and a one-minute harness smoke passed. The required eight-hour run is not captured. | Incomplete |
| Rendered UI virtualization trace | `docs/desktop-virtualization-trace.md` defines the trace checklist and `scripts/capture_desktop_virtualization_trace.ps1` standardizes process metric capture. XAML guards prove bounded `ListBox` surfaces with explicit `VirtualizingStackPanel` panels for large library/search/playlist lists, but a rendered UI trace is not captured. | Incomplete |
| Docker/headless smoke | Docker CLI is installed, but `docker version` cannot reach `dockerDesktopLinuxEngine` (`open //./pipe/dockerDesktopLinuxEngine: The system cannot find the file specified.`). | Environment-blocked |

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
- Ask testers to use redacted diagnostics exports and never paste tokens, OAuth codes, client secrets, Soulseek passwords or full Authorization headers.
- Do not present the build as a public beta or stable release.
