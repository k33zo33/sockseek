## Sprint 10 - Spotify playlist import

## Status

Planned

## Required context

Always read `/AGENTS.md` and `/docs/project-state.yaml`, then:

- [PROVIDERS.md](../PROVIDERS.md)
- [SECURITY.md](../SECURITY.md)

## Scope rule

Do not implement future sprint scope. Stop and request an ADR if a locked decision must change.

> **Cilj sprinta**  
> Omogućiti allowlistanim korisnicima povezivanje Spotify računa i uvoz/sinkronizaciju playlista bez Spotify playbacka.

Ovisnosti: Sprint 9.

### Isporučivi rezultati

- Spotify PKCE adapter.

- Playlist list/details import i pagination.

- Development-mode/quota UX.

- Spotify provider fixtures i sync testovi.

### Implementacijski zadaci

1. Registrirati minimalne readonly scopeove: playlist-read-private i playlist-read-collaborative; user-library-read samo ako se implementira saved tracks import.

1. Mapirati track, episode/unsupported item i unavailable item slučajeve.

1. Sačuvati ISRC, external ID, URL, artist, album, duration i artwork metadata.

1. Implementirati Copy i Mirror import.

1. Obraditi 401 refresh, 403 not allowlisted, 429 Retry-After i pagination.

1. Dodati “Open in Spotify” samo kao vanjski link, bez play kontrole.

### Acceptance kriteriji

- Allowlistani korisnik vidi i uvozi svoje playliste.

- Neallowlistani 403 ima razumljivu poruku o Spotify development modeu.

- Ponovljeni sync ne duplicira stavke i čuva local resolution status.

- Aplikacija nema Spotify player niti audio endpoint.

- Disconnect briše credential i playlist snapshot ostaje kao lokalna kopija po korisničkoj odluci.

### Obavezni testovi

- Recorded HTTP fixture tests.

- Pagination, 401, 403, 429 tests.

- Mirror sync diff tests.

- UI no-playback assertion test.

> **Izlazni artefakt sprinta**  
> Spotify playlist source funkcionalan u ograničenom beta okruženju.

## Completion report

Report changed files, validation commands and results, migrations, security/license impact, known risks and every unmet acceptance criterion.
