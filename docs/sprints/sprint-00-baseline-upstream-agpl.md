## Sprint 0 - Baseline, upstream sync i AGPL setup

## Status

Completed

## Required context

Always read `/AGENTS.md` and `/docs/project-state.yaml`, then:

- [BASELINE.md](../BASELINE.md)
- [LEGAL.md](../LEGAL.md)
- [PRODUCT.md](../PRODUCT.md)
- [0001-agpl-path-a.md](../adr/0001-agpl-path-a.md)
- [0002-local-first.md](../adr/0002-local-first.md)
- [0003-external-providers-playlist-only.md](../adr/0003-external-providers-playlist-only.md)

## Scope rule

Do not implement future sprint scope. Stop and request an ADR if a locked decision must change.

> **Cilj sprinta**  
> Stvoriti reproducibilnu, pravno i tehnički jasnu početnu točku prije funkcionalnih promjena.

Ovisnosti: Nema.

### Isporučivi rezultati

- Tag baseline commita i fast-forward forka na pregledani upstream.

- docs/adr/0001-agpl-product.md, 0002-local-first.md i 0003-provider-playlist-only.md.

- Ažurirani README s novom product vizijom bez obećanja vanjskog streaminga.

- CI baseline report i popis poznatih warninga.

### Implementacijski zadaci

1. Napraviti sigurnosnu kopiju grane i tag baselinea.

1. Sinkronizirati upstream u zasebnoj codex grani i pokrenuti puni test suite.

1. Provjeriti LICENSE, copyright i third-party licence.

1. Dodati docs/product-scope.md iz zaključanih odluka ovog dokumenta.

1. Dodati docs/provider-capability-matrix.md.

1. Uskladiti i validirati Docker headless packaging put za `net10.0`, te dokumentirati preostali daemon/compose review scope.

### Acceptance kriteriji

- master nije force-pushan.

- dotnet build i svi postojeći testovi prolaze na sinkroniziranom baselineu.

- Repo sadrži tri ADR-a i product scope.

- Dokumentacija eksplicitno zabranjuje provider playback/download.

- AGPL source/link zahtjevi nalaze se u release checklisti.

### Obavezni testovi

- Puni postojeći test suite.

- CI run na Linuxu.

- Ručna provjera da legacy CLI help radi.

> **Izlazni artefakt sprinta**  
> PR koji samo stabilizira osnovu i dokumentira odluke; bez UI funkcionalnosti.

## Completion report

Report changed files, validation commands and results, migrations, security/license impact, known risks and every unmet acceptance criterion.
