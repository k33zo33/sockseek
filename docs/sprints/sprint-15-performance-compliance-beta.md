## Sprint 15 - Performance, Soulseek compliance i beta stabilizacija

## Status

Completed

## Required context

Always read `/AGENTS.md` and `/docs/project-state.yaml`, then:

- [TESTING.md](../TESTING.md)
- [LEGAL.md](../LEGAL.md)
- [SECURITY.md](../SECURITY.md)
- [TRACEABILITY.md](../TRACEABILITY.md)

## Scope rule

Do not implement future sprint scope. Stop and request an ADR if a locked decision must change.

> **Cilj sprinta**  
> Odlučiti je li aplikacija spremna za javnu beta distribuciju i ukloniti najveće operativne rizike.

Ovisnosti: Sprint 14.

### Isporučivi rezultati

- Performance report i optimizacije.

- Soulseek client feature/compliance audit.

- Public beta checklist i known limitations.

- Crash/diagnostic feedback proces.

### Implementacijski zadaci

1. Testirati 100k library zapisa, 10k playlistu i velike Soulseek result setove.

1. Profilirati event traffic, DB queryje, image cache i UI virtualizaciju.

1. Auditirati postojeći Soulseek.NET/Sockseek feature set prema pravilima: search, wishlist, download, upload, chat, privileges i sharing.

1. Donijeti ADR: javni beta, zatvoreni beta ili dodatni compliance sprintovi.

1. Dokumentirati legal-use poruku i user responsibility bez impliciranja legitimnosti svakog sadržaja.

1. Definirati issue template, security reporting i reproducible diagnostics.

### Acceptance kriteriji

- Nema nekontroliranog memory growtha u osmosatnom runu.

- 100k library search i virtualizirani prikaz zadovoljavaju definirani performance budget.

- Postoji pisana Soulseek compliance odluka; javni release nije dopušten bez nje.

- Svi provider rate-limit i token-expiry scenariji imaju recovery UX.

- Release notes jasno navode ograničenja Spotify quota i Bandcamp/MusicBrainz mogućnosti.

### Obavezni testovi

- Soak tests.

- Large data benchmarks.

- End-to-end failure matrix.

- Packaged beta smoke test na ciljanim OS-ovima.

> **Izlazni artefakt sprinta**  
> Go/no-go dokument za javnu beta verziju i stabilan beta build ako su svi gateovi zadovoljeni.

## Completion report

Completed on 2026-10-18 with a public-beta `NO-GO` and closed/internal-beta `CONDITIONAL GO` decision.

- Performance evidence: the post-fix eight-hour soak passed its managed/private-memory budgets; deterministic 100k library, 10k playlist, large Soulseek result and event-traffic budgets passed; rendered Library, Search and Playlist traces from exact Windows package commit `6da97fd` passed together with 90 samples per surface.
- Compliance and release evidence: ADR-0009, the Soulseek audit/checklist, provider recovery matrix, beta limitations, legal-use guidance, diagnostics/security process, Docker smoke and Windows RC package smoke are complete and validator-backed.
- Runtime hardening: packaged Desktop startup, bundled daemon discovery, Windows STA startup, abandoned SQLite migration-lock recovery, recoverable Desktop transport failures, responsive playlist rows and missed/transient local-library watcher recovery were fixed with regression coverage.
- Validation: `dotnet restore`, `dotnet build -c Release` (`0` warnings/errors), and `dotnet test -c Release --no-build` passed with Architecture 5, Domain 26, Application 55, Packager 13, Infrastructure 76, Core 579, CLI 258, Player 34, Server 164 and Desktop 242 tests. All Sprint 15 validators and the provider-audio guard passed.
- Migrations: none added. Startup migration ordering and abandoned-lock behavior changed without changing the schema.
- Security/license impact: no secret handling, loopback auth, provider scope or AGPL decision changed; package evidence retains `LICENSE`, `SECURITY.md`, `THIRD-PARTY-NOTICES`, exact source metadata and redacted diagnostics guidance.
- Remaining public-release blockers: ADR-0009 remains active because wishlist, upload/sharing, chat and privilege coverage are missing; Windows 11 and Ubuntu target-OS package smoke and exact public release notes/tag evidence are also incomplete. These are carried into Sprint 16 and public beta remains prohibited.
