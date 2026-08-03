## Sprint 9 - Provider framework i secret store

## Status

Planned

## Required context

Always read `/AGENTS.md` and `/docs/project-state.yaml`, then:

- [PROVIDERS.md](../PROVIDERS.md)
- [SECURITY.md](../SECURITY.md)
- [DATABASE.md](../DATABASE.md)

## Scope rule

Do not implement future sprint scope. Stop and request an ADR if a locked decision must change.

> **Cilj sprinta**  
> Postaviti siguran i capability-driven temelj prije dodavanja stvarnih account providera.

Ovisnosti: Sprintovi 1, 3 i 4.

### Isporučivi rezultati

- IPlaylistSourceProvider i capability registry.

- OAuth coordinator s PKCE/state/loopback callbackom.

- ISecretStore platform abstraction.

- Accounts UI i provider status modeli.

### Implementacijski zadaci

1. Implementirati fake provider za E2E testove.

1. Implementirati Windows secret store; Linux/macOS adaptere planirati ili implementirati prema targetu.

1. Dodati provider HTTP pipeline s retry/429/backoff pravilima.

1. Dodati log redaction handler.

1. Implementirati connect/disconnect lifecycle i expired state.

1. UI mora capabilityjima sakriti nepodržane akcije.

### Acceptance kriteriji

- Access i refresh token ne postoje u SQLiteu ni logovima.

- PKCE state mismatch se odbija.

- Fake provider može importirati i sinkronizirati playlistu.

- Disconnect briše secret i account status se ažurira.

- Bandcamp capability ne prikazuje Connect account.

### Obavezni testovi

- OAuth callback adversarial tests.

- Secret store integration tests.

- Redaction tests.

- Provider capability UI tests.

> **Izlazni artefakt sprinta**  
> Siguran provider framework spreman za Spotify i YouTube bez dupliciranja auth logike.

## Completion report

Report changed files, validation commands and results, migrations, security/license impact, known risks and every unmet acceptance criterion.
