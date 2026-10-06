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

## In scope

- Add deterministic large-data benchmarks/tests for library search/projection, playlist detail/query paths and large Soulseek result projection.
- Run or document opt-in soak/profiling commands with budgets and captured results.
- Audit Soulseek/Sockseek feature coverage against search, wishlist, download, upload, chat, privileges and sharing expectations.
- Add an ADR deciding public beta, closed beta or more compliance work.
- Document beta limitations, legal-use/user-responsibility messaging and provider quota/rate-limit caveats.
- Improve issue/security reporting templates and reproducible diagnostics instructions.
- Add missing provider failure-matrix tests where current fixture coverage is incomplete.

## Out of scope

- Rewriting the Soulseek engine or implementing a new Soulseek protocol client.
- Adding provider audio playback, provider download capability or provider write-back.
- Remote daemon mode, mobile/web companion control or centralized hosted Soulseek service.
- Auto-update, code signing and cross-platform stable packaging beyond beta smoke documentation.

## Files and projects affected

- `Sockseek.Benchmarks` for large-data benchmark fixtures and reports.
- `Sockseek.Infrastructure.Tests`, `Sockseek.Server.Tests` and `Sockseek.Desktop.Tests` for performance/failure-matrix coverage.
- `docs/adr/*`, `docs/PRODUCT.md`, `docs/PROVIDERS.md`, `docs/release-checklist.md`, `docs/TRACEABILITY.md` and a new beta limitations/report document.
- `.github/ISSUE_TEMPLATE/*` and root `SECURITY.md` for support/security reporting.
- Possibly `.github/workflows/ci.yml` if a lightweight non-soak performance gate is safe for CI.

## API, Schema and Event Changes

- Prefer no public API or database schema changes.
- If diagnostics endpoints or DTOs need expansion, expose only non-secret operational state and update `docs/openapi.json`.
- Event traffic changes must preserve existing SignalR event contracts unless a compatibility note is documented.

## Implementation Sequence

1. Establish Sprint 15 baseline reports: performance targets, Soulseek compliance checklist and beta go/no-go outline.
2. Add large-data benchmark/test fixtures for 100k library search and 10k playlist query/projection paths.
3. Run event traffic profiling and add budget documentation for the opt-in soak commands.
4. Audit provider rate-limit/token-expiry/forbidden/malformed scenarios and add missing recovery tests or docs.
5. Audit Soulseek feature/compliance coverage and write the beta decision ADR.
6. Add root `SECURITY.md`, improve issue templates and document reproducible diagnostics/log export guidance.
7. Run full validation, package smoke where feasible and provider-audio guard scan before closing Sprint 15.

## Testing Strategy

- Baseline: `dotnet restore`, `dotnet build -c Release`, `dotnet test -c Release --no-build`.
- Benchmark smoke: run selected `Sockseek.Benchmarks` jobs with `QuickBenchmarkConfig` or documented filters.
- Opt-in profiling: run `SOCKSEEK_RUN_EVENT_PROFILE=1` server tests for event traffic budgets outside the default suite.
- Add deterministic fixture tests for large library/playlist query behavior that can remain in the normal test suite.
- Keep provider-audio forbidden-symbol scans in every final Sprint 15 validation pass.

## Migration and Rollback

- No persistence migration is expected.
- Large benchmark fixtures should be generated in temp directories or in-memory databases and cleaned after runs.
- Rollback removes benchmark/test/docs additions without touching user data, packaging outputs or provider credentials.

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

## Acceptance-Criteria Mapping

- No uncontrolled eight-hour memory growth: soak/profiling command, result capture and known-limits doc.
- 100k library search and virtualized display budget: benchmark/test fixtures plus Desktop or query-store evidence.
- Written Soulseek compliance decision: ADR with go/no-go outcome.
- Provider recovery UX: failure matrix tests and documentation for rate-limit/token-expiry flows.
- Release notes limitations: beta limitations document covering Spotify quota and Bandcamp/MusicBrainz behavior.
