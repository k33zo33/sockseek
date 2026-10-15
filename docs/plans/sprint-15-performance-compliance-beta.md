# Sprint 15 Performance, Compliance and Beta Stabilization

## Goal

Decide whether Sockseek is ready for public beta distribution, with measured performance evidence, a written Soulseek compliance decision, provider failure recovery coverage and a reproducible beta-support process.

## Current-state findings

- Sprint 14 produced a Windows release-candidate path with package smoke, SBOM, checksum manifest and legal/security artifacts.
- `Sockseek.Benchmarks` exists, but it is still mostly Core/search-string focused and does not yet cover the Sprint 15 100k library, 10k playlist or provider-resolution paths.
- `Sockseek.Server.Tests/EventTrafficProfilingTests.cs` already has opt-in event traffic profiling with budgets for large workflow updates.
- Sprint 6 documented 10k library coverage; Sprint 15 raises the target to 100k library entries and a 10k playlist.
- `.github/ISSUE_TEMPLATE` has minimal bug/feature templates, but no root `SECURITY.md` or beta diagnostic/support checklist.
- Existing provider tests cover some token expiry and forbidden/quota cases, but Sprint 15 requires a documented failure matrix and recovery UX check across providers.
- The product docs already state that public release is blocked on Soulseek compliance, but there is no beta go/no-go ADR yet.
- Launching the packaged Windows Desktop for rendered-trace capture exposed a startup shutdown crash: the named mutex lease can be disposed after an `await` on a different thread, while `Mutex.ReleaseMutex` requires the acquiring thread.
- The same packaged launch also exposed that the main window was constructed inside Avalonia's lifetime-configuration callback before platform setup, so `IWindowingPlatform` was unavailable.

## In scope

- Add deterministic large-data benchmarks/tests for library search/projection, playlist detail/query paths and large Soulseek result projection.
- Add an opt-in rendered Desktop trace fixture preparation and launch path that seeds an isolated config directory, configures mock Soulseek files and starts Desktop with those paths without marking the manual rendered trace gate as passed.
- Run or document opt-in soak/profiling commands with budgets and captured results.
- Audit Soulseek/Sockseek feature coverage against search, wishlist, download, upload, chat, privileges and sharing expectations.
- Add an ADR deciding public beta, closed beta or more compliance work.
- Document beta limitations, legal-use/user-responsibility messaging and provider quota/rate-limit caveats.
- Improve issue/security reporting templates and reproducible diagnostics instructions.
- Add missing provider failure-matrix tests where current fixture coverage is incomplete.
- Fix the packaged Desktop single-instance gate so lease disposal remains valid across asynchronous thread switches while preserving one-instance behavior.
- Create the Avalonia main window only after platform setup while preserving cancellation-driven shutdown and `OnMainWindowClose` behavior.

## Out of scope

- Rewriting the Soulseek engine or implementing a new Soulseek protocol client.
- Adding provider audio playback, provider download capability or provider write-back.
- Remote daemon mode, mobile/web companion control or centralized hosted Soulseek service.
- Auto-update, code signing and cross-platform stable packaging beyond beta smoke documentation.

## Files and projects affected

- `Sockseek.Benchmarks` for large-data benchmark fixtures and reports.
- `Sockseek.Infrastructure.Tests`, `Sockseek.Server.Tests` and `Sockseek.Desktop.Tests` for performance/failure-matrix coverage.
- `scripts/prepare_desktop_virtualization_fixture.ps1`, `scripts/start_desktop_virtualization_fixture.ps1` and Desktop daemon-launch tests for repeatable rendered-trace fixture setup.
- `Sockseek.Desktop` and `Sockseek.Desktop.Tests` for the async-safe single-instance gate and its regression coverage.
- `docs/adr/*`, `docs/PRODUCT.md`, `docs/PROVIDERS.md`, `docs/release-checklist.md`, `docs/TRACEABILITY.md` and a new beta limitations/report document.
- `.github/ISSUE_TEMPLATE/*` and root `SECURITY.md` for support/security reporting.
- Possibly `.github/workflows/ci.yml` if a lightweight non-soak performance gate is safe for CI.

## API, Schema and Event Changes

- Prefer no public API or database schema changes.
- Additive status DTO fields are allowed when they expose existing non-secret Soulseek operational state needed for beta compliance review, such as configured share counts and listen-port/sharing indicators. Update OpenAPI and focused tests in the same change.
- A development-only Desktop daemon launch environment override may point the daemon at a trace fixture config directory; packaged defaults must remain unchanged.
- If diagnostics endpoints or DTOs need expansion, expose only non-secret operational state and update `docs/openapi.json`.
- Event traffic changes must preserve existing SignalR event contracts unless a compatibility note is documented.

## Implementation Sequence

1. Establish Sprint 15 baseline reports: performance targets, Soulseek compliance checklist and beta go/no-go outline.
2. Add large-data benchmark/test fixtures for 100k library search and 10k playlist query/projection paths.
3. Run event traffic profiling and add budget documentation for the opt-in soak commands.
4. Audit provider rate-limit/token-expiry/forbidden/malformed scenarios and add missing recovery tests or docs.
5. Audit Soulseek feature/compliance coverage and write the beta decision ADR.
6. Add root `SECURITY.md`, improve issue templates and document reproducible diagnostics/log export guidance.
7. Add rendered Desktop trace fixture preparation and launch helpers so the operator can load 100k library, 10k playlist and 10k mock Soulseek search fixtures consistently before capture.
8. Fix packaged Desktop startup failures discovered while exercising the rendered trace path, with focused regression tests.
9. Run full validation, package smoke where feasible and provider-audio guard scan before closing Sprint 15.

## Testing Strategy

- Baseline: `dotnet restore`, `dotnet build -c Release`, `dotnet test -c Release --no-build`.
- Benchmark smoke: run selected `Sockseek.Benchmarks` jobs with `QuickBenchmarkConfig` or documented filters.
- Opt-in profiling: run `SOCKSEEK_RUN_EVENT_PROFILE=1` server tests for event traffic budgets outside the default suite.
- Add deterministic fixture tests for large library/playlist query behavior that can remain in the normal test suite.
- Add opt-in fixture-writer smoke coverage and script tests that run with tiny counts by default; full 100k/10k trace preparation remains explicit, and launch-helper tests must use print-only mode rather than opening a GUI.
- Add a cross-thread lease-disposal test for the concrete Desktop single-instance gate and retain the existing second-instance rejection/reacquire tests.
- Guard the Avalonia startup source order so the main window remains in the post-platform-setup callback, then verify a real Windows main-window handle from the packaged executable.
- Keep provider-audio forbidden-symbol scans in every final Sprint 15 validation pass.

## Migration and Rollback

- No persistence migration is expected.
- Large benchmark fixtures should be generated in temp directories or in-memory databases and cleaned after runs.
- Rendered trace fixture data should default to `.tmp/` and require an explicit force/overwrite path before replacing an existing fixture. The launch helper must only pass fixture paths through process environment variables and must not persist them globally.
- Rollback removes benchmark/test/docs additions without touching user data, packaging outputs or provider credentials.
- The single-instance implementation can be rolled back independently; it has no persisted state or schema impact.

## Security, Privacy and License Impact

- Beta docs must clearly state legal-use and user responsibility without implying legitimacy of every downloadable item.
- Security reporting must avoid asking users to paste secrets, tokens, Soulseek passwords or full logs.
- Diagnostics guidance must use the redacted export path from Sprint 14.
- Soulseek compliance decision is a release blocker: public beta must not proceed without an accepted ADR.

## Risks and Stop Conditions

- Stop and request an ADR if compliance work requires upload/chat/sharing behavior that changes product scope materially.
- Stop if performance fixes require provider audio, remote daemon mode, centralized service behavior or broader process topology changes.
- Treat uncontrolled memory growth, failing large-data budgets or missing compliance decision as beta blockers.
- Do not mark Sprint 15 complete if soak/performance results are only asserted and not captured in docs or tests.
- Do not mark rendered UI virtualization complete from fixture preparation alone; only captured rendered reports can satisfy that gate.
- Stop if the single-instance fix weakens process-wide exclusion or requires a process-topology change; neither is expected for replacing the thread-affine primitive behind the existing gate contract.

## Acceptance-Criteria Mapping

- No uncontrolled eight-hour memory growth: soak/profiling command, result capture and known-limits doc.
- 100k library search and virtualized display budget: benchmark/test fixtures, repeatable rendered-trace fixture setup and captured Desktop trace evidence before public beta.
- Written Soulseek compliance decision: ADR with go/no-go outcome.
- Provider recovery UX: failure matrix tests and documentation for rate-limit/token-expiry flows.
- Release notes limitations: beta limitations document covering Spotify quota and Bandcamp/MusicBrainz behavior.
