## Sprint 14 - Packaging, pravne obavijesti i security hardening

## Status

Planned

## Required context

Always read `/AGENTS.md` and `/docs/project-state.yaml`, then:

- [LEGAL.md](../LEGAL.md)
- [SECURITY.md](../SECURITY.md)
- [TESTING.md](../TESTING.md)

## Scope rule

Do not implement future sprint scope. Stop and request an ADR if a locked decision must change.

> **Cilj sprinta**  
> Izraditi release candidate koji se sigurno instalira i ispunjava AGPL obveze.

Ovisnosti: Svi funkcionalni MVP sprintovi.

### Isporučivi rezultati

- Windows self-contained installer; Linux package nakon Windows stabilnosti.

- Ažurirani .NET 10 daemon Dockerfile.

- About/License/Source UI.

- Threat model, SBOM i dependency scan.

### Implementacijski zadaci

1. Pakirati Desktop, daemon, SQLite native i media native dependencies.

1. Implementirati user-data direktorije i upgrade backup.

1. Dodati log export s redactionom.

1. Dodati source commit/version u About i API info.

1. Dodati LICENSE i THIRD-PARTY-NOTICES u paket.

1. Auditirati file traversal, localhost auth, OAuth callback i secret deletion.

1. Dodati crash recovery i clean shutdown child procesa.

### Acceptance kriteriji

- Čista Windows instalacija pokreće aplikaciju bez ručne .NET instalacije.

- Update ne briše bazu, config ili glazbu.

- About prikazuje AGPL, source link, version i commit.

- Secrets nisu u log exportu.

- Daemon nije otvoren prema LAN-u po defaultu.

- SBOM i third-party notices dio su release artefakta.

### Obavezni testovi

- Fresh install/upgrade/uninstall smoke tests.

- Security integration tests.

- License/source artifact checklist test.

- Path traversal and symlink tests.

> **Izlazni artefakt sprinta**  
> Potpisan ili interno verificiran release candidate s kompletnim source/legal paketom.

## Completion report

Report changed files, validation commands and results, migrations, security/license impact, known risks and every unmet acceptance criterion.
