## Sprint 15 - Performance, Soulseek compliance i beta stabilizacija

## Status

Planned

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

Report changed files, validation commands and results, migrations, security/license impact, known risks and every unmet acceptance criterion.
