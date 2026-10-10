# Sprint 15 beta go/no-go

Last updated on 2026-10-09.

## Decision

| Release type | Decision | Reason |
| --- | --- | --- |
| Public beta | NO-GO | ADR-0009 blocks public beta until Soulseek compliance coverage is resolved. Rendered UI virtualization trace and Docker/headless smoke remain incomplete. |
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
| Provider recovery UX | `docs/provider-failure-matrix.md` documents recovery paths; provider endpoint tests cover Spotify/YouTube rate-limit and token/error surfaces plus Bandcamp malformed public import recovery without account state. | Passed |
| Release limitations | `docs/beta-limitations.md` states Spotify quota limits, Bandcamp/MusicBrainz scope and provider no-audio policy. `scripts/write_closed_beta_tester_instructions.ps1` generates per-build closed/internal beta tester instructions with exact commit/source, legal-use notice, limitations and diagnostics redaction guidance. | Passed |
| Provider-audio scope guard | `scripts/run_provider_audio_guard.ps1` passed on 2026-10-09, scanning 428 production source files and 278 provider-boundary files for forbidden provider playback/download/audio markers while preserving legacy Core/CLI compatibility boundaries. | Passed |
| Security and crash reporting | Root `SECURITY.md`, `docs/diagnostics-feedback.md` and GitHub issue templates route private vulnerabilities, crash reports and redacted diagnostics. | Passed |
| Dependency vulnerability scan | `dotnet list package --vulnerable --include-transitive` passed on 2026-10-10 for commit `66a6e03` using `https://api.nuget.org/v3/index.json` and the local Microsoft SDK package source; no project reported known vulnerable packages. | Passed |
| Full solution validation | `dotnet build -c Release` passed on commit `04ab151` with `0 Warning(s), 0 Error(s)`. `dotnet test -c Release --no-build` passed on commit `04ab151` across all test projects: Architecture 5, Application 55, Domain 26, Packager 13, Infrastructure 74, Core 579, Player 34, CLI 258, Desktop 229 and Server 159 tests. Release build regenerated `docs/openapi.json` with no Git drift, and `scripts/run_provider_audio_guard.ps1` passed. | Passed |
| Windows package smoke | `scripts/run_windows_package_smoke.ps1 -Force` passed for commit `15668e1`: Windows Desktop and daemon publish, legal files, release metadata, 126-package SBOM, staging validation, SHA256 manifest, Desktop and daemon executables and native dependencies. Staging validation now requires `SECURITY.md` and `docs/beta-limitations.md`, and the SHA256 manifest includes both files. This run emitted `NU1900` package-vulnerability-source warnings because `https://api.nuget.org/v3/index.json` was unavailable from the validation session; dependency vulnerability scan remains a separate gate. | Passed for Windows RC with noted feed warning |
| Soak stability | `SoakStabilityTests.RepeatedWorkflowSoak_DoesNotExceedMemoryGrowthBudget` exists, `scripts/run_sprint15_soak.ps1` standardizes the opt-in run, JSON report artifact and transcript log artifact. An eight-hour local run on 2026-10-09 against commit `acea78d` completed 952 cycles but failed managed heap growth at `346.93 MiB` against the `256.00 MiB` budget. Core workflow pruning is now tied to server history eviction; the post-fix eight-hour run on commit `93a8c63` completed 992 cycles, held retained workflows at 250 after the cap, and passed with managed growth `146.33 MiB` against the `256.00 MiB` budget and private growth `241.13 MiB` against the `512.00 MiB` budget. | Passed |
| Rendered UI virtualization trace | `docs/desktop-virtualization-trace.md` defines the trace checklist, `scripts/capture_desktop_virtualization_trace.ps1` standardizes process metric capture, and `scripts/validate_desktop_virtualization_traces.ps1` validates the three required reports before the gate is recorded. The capture helper records whether a main window was present and whether `-AllowHeadlessProcess` was used; the validator rejects headless captures so harness smoke cannot be mistaken for rendered UI evidence. XAML guards prove bounded `ListBox` surfaces with explicit `VirtualizingStackPanel` panels for large library/search/playlist lists, but the required rendered Desktop traces are not captured. | Incomplete |
| Docker/headless smoke | `scripts/run_docker_smoke.ps1` standardizes compose validation, image build, CLI help and daemon `/health` checks, and bounds each Docker CLI command with `-DockerCommandTimeoutSeconds` so engine hangs fail with a clear timeout. Cleanup now uses the same bounded Docker invocation path, and `-DiagnosticsPath` writes a Markdown artifact with user, PowerShell, Docker context, command output and timeout evidence. Adding `.dockerignore` reduced build context from more than `3.35 GiB` to about `66 KiB`; `Dockerfile` now restores `Sockseek.HelpGenerator` and installs `vlc-libs` for daemon startup. The latest local rerun wrote diagnostics showing `desktop-linux` selected, but `docker version` still timed out after `5s` from this session. Full build/run validation remains incomplete until the Docker engine responds and the helper passes end to end. | Environment-blocked |

## Required before public beta

1. Resolve ADR-0009 by either implementing the missing Soulseek compliance work or accepting a superseding decision.
2. Capture the rendered Desktop UI virtualization trace described in `docs/desktop-virtualization-trace.md`.
3. Complete target OS package smoke for every public beta artifact, including Docker/headless validation once the local Docker engine responds.
4. Update release notes with the exact commit/tag, beta limitations, legal-use notice and source availability.

## Closed beta requirements

- Distribute only to internal or allowlisted testers.
- Include `docs/beta-limitations.md`, root `SECURITY.md`, `LICENSE` and `THIRD-PARTY-NOTICES`.
- Identify the exact commit in tester instructions; use `scripts/write_closed_beta_tester_instructions.ps1` for reproducible per-build instructions.
- Ask testers to use the `Copy diagnostics` action when it is available and never paste tokens, OAuth codes, client secrets, Soulseek passwords or full Authorization headers.
- Do not present the build as a public beta or stable release.
