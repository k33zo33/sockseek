## Sprint 11 - YouTube playlist import

## Status

Planned

## Required context

Always read `/AGENTS.md` and `/docs/project-state.yaml`, then:

- [PROVIDERS.md](../PROVIDERS.md)
- [SECURITY.md](../SECURITY.md)

## Scope rule

Do not implement future sprint scope. Stop and request an ADR if a locked decision must change.

> **Cilj sprinta**  
> Povezati Google/YouTube račun i uvesti playlist metadata bez reprodukcije ili preuzimanja YouTube audija.

Ovisnosti: Sprint 9.

### Isporučivi rezultati

- Google installed-app OAuth adapter.

- YouTube playlist i playlistItems import.

- Quota/pagination/error UX.

- Policy guard testovi koji zabranjuju audio funkcije.

### Implementacijski zadaci

1. Koristiti readonly YouTube scope i system browser/loopback redirect.

1. Dohvatiti korisničke playliste autoriziranim mine=true zahtjevom.

1. Dohvatiti sve playlist items s paginationom.

1. Mapirati video ID, title, channel, duration ako je dostupna kroz dodatni batch lookup, thumbnail i URL.

1. Obraditi deleted/private/unavailable video kao unresolved metadata item.

1. Ne dodavati yt-dlp ni iframe/video player u novi UI.

### Acceptance kriteriji

- Korisnik uvozi svoje YouTube playliste kao lokalne playliste.

- Private/deleted stavke ostaju vidljive s jasnim statusom, bez crasha.

- Nema YouTube audio, download ili background playback koda u novim projektima.

- Token expiry i revoke vode account u ReauthorizationRequired.

### Obavezni testovi

- OAuth fixture tests.

- Playlist pagination tests.

- Deleted/private item tests.

- Static architecture/policy test koji zabranjuje download/playback metode u YouTube projektu.

> **Izlazni artefakt sprinta**  
> YouTube je potpuno podržan kao source playlista, bez audio policy rizika u aplikaciji.

## Completion report

Report changed files, validation commands and results, migrations, security/license impact, known risks and every unmet acceptance criterion.
