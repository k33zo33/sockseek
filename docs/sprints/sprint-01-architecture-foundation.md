## Sprint 1 - Arhitekturni foundation

## Status

Planned

## Required context

Always read `/AGENTS.md` and `/docs/project-state.yaml`, then:

- [ARCHITECTURE.md](../ARCHITECTURE.md)
- [TESTING.md](../TESTING.md)

## Scope rule

Do not implement future sprint scope. Stop and request an ADR if a locked decision must change.

> **Cilj sprinta**  
> Dodati nove projekte i dependency granice bez promjene ponašanja postojećeg enginea.

Ovisnosti: Sprint 0.

### Isporučivi rezultati

- Novi Domain, Application, Infrastructure, Integrations.Abstractions, Player i Desktop skeleton projekti.

- Central package management i architecture tests.

- Osnovni /api/v1/system/info i /health endpointi.

- App correlation ID i structured error envelope.

### Implementacijski zadaci

1. Kreirati projekte i reference prema pravilima ovisnosti.

1. Dodati Result/Error tipove za application use caseove.

1. Uvesti IClock, IIdGenerator i IFileSystem apstrakcije gdje testabilnost to zahtijeva.

1. Dodati globalno exception mapiranje za novi API.

1. Dodati OpenAPI drift test.

1. Postaviti build za Desktop skeleton bez pokretanja enginea.

### Acceptance kriteriji

- Domain nema package reference na EF, Avalonia, provider SDK ili Sockseek.Core.

- Server vraća version, commit i capability snapshot.

- Architecture tests padaju ako Desktop referencira Core.

- Legacy API i CLI testovi ostaju zeleni.

### Obavezni testovi

- Architecture dependency tests.

- System endpoint integration test.

- OpenAPI snapshot test.

> **Izlazni artefakt sprinta**  
> Solution koja se gradi s novim praznim slojevima i dokazanim dependency pravilima.

## Completion report

Report changed files, validation commands and results, migrations, security/license impact, known risks and every unmet acceptance criterion.
