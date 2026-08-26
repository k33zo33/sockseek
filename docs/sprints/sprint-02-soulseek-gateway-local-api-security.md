## Sprint 2 - Soulseek gateway i lokalni API security

## Status

Completed

## Required context

Always read `/AGENTS.md` and `/docs/project-state.yaml`, then:

- [ARCHITECTURE.md](../ARCHITECTURE.md)
- [API.md](../API.md)
- [SECURITY.md](../SECURITY.md)

## Scope rule

Do not implement future sprint scope. Stop and request an ADR if a locked decision must change.

> **Cilj sprinta**  
> Uvesti stabilnu application granicu prema postojećem engineu i zaštititi lokalni daemon.

Ovisnosti: Sprint 1.

### Isporučivi rezultati

- ISoulseekEngineGateway i implementacija nad postojećim EngineSupervisor/API slojem.

- Immutable JobSnapshot i EngineEventEnvelope modeli.

- Local session-token autentikacija.

- Gateway parity testovi.

### Implementacijski zadaci

1. Mapirati track search, album search, download, cancel i next-candidate.

1. Uvesti event coalescing za progress.

1. Dodati session token middleware samo za /api/v1 i osjetljive endpointove; health može imati ograničeni anonimni odgovor.

1. Ograničiti default bind na loopback.

1. Dodati correlation/workflow mapiranje.

1. Dodati fake gateway za application testove.

### Acceptance kriteriji

- Novi slojevi ne primaju Core Job objekte.

- Track search i download kroz gateway daju isti konačni rezultat kao legacy backend fixture.

- Zahtjev bez session tokena dobiva 401.

- Daemon nije dostupan preko LAN interfejsa u default konfiguraciji.

- Cancel i next-candidate rade kroz novi API.

### Obavezni testovi

- Local/remote parity tests.

- Auth middleware integration tests.

- Event serialization/reconnect tests.

> **Izlazni artefakt sprinta**  
> Stabilni gateway na kojem svi sljedeći sprintovi mogu graditi bez poznavanja Core internala.

## Completion report

Report changed files, validation commands and results, migrations, security/license impact, known risks and every unmet acceptance criterion.
