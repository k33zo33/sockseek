## Sprint 12 - Bandcamp public URL i MetaBrainz metadata

## Status

Planned

## Required context

Always read `/AGENTS.md` and `/docs/project-state.yaml`, then:

- [PROVIDERS.md](../PROVIDERS.md)
- [DOMAIN_MODEL.md](../DOMAIN_MODEL.md)

## Scope rule

Do not implement future sprint scope. Stop and request an ADR if a locked decision must change.

> **Cilj sprinta**  
> Dodati službeno realan Bandcamp import put i MusicBrainz enrichment; jasno razdvojiti playlist source od metadata providera.

Ovisnosti: Sprintovi 3 i 9.

### Isporučivi rezultati

- Bandcamp public URL importer iza nestabilnosti guardova.

- MusicBrainz API client s limiterom i cacheom.

- MBID/ISRC enrichment queue.

- Opcionalni ListenBrainz ADR/prototype za korisničke playliste.

### Implementacijski zadaci

1. Bandcamp: validirati URL, dohvatiti javni album/track metadata i mapirati tracklistu; ne koristiti login/cookies.

1. Bandcamp parser izolirati iza adaptera i fixture HTML/JSON testova.

1. MusicBrainz: smislen User-Agent, globalni 1 req/s limiter, retry 503 i local cache.

1. Implementirati recording lookup po ISRC-u i fuzzy search samo kao enrichment, ne autoritativni auto-match bez scorea.

1. UI za MusicBrainz ne prikazuje “Connect account” za playlist import.

1. Napraviti ADR hoće li se ListenBrainz uključiti za MetaBrainz korisničke playliste u post-MVP fazi.

### Acceptance kriteriji

- Javni Bandcamp album URL postaje lokalna playlista.

- Promjena parsera ne ruši ostale providere; greška je lokalizirana na import.

- Nema Bandcamp credentials/cookies pohranjenih u aplikaciji.

- MusicBrainz nikad ne prelazi limiter u testiranom scheduleru.

- MBID/ISRC se spremaju i koriste u TrackIdentityServiceu.

### Obavezni testovi

- Bandcamp fixture parser tests.

- MusicBrainz limiter/cache tests.

- ISRC/MBID enrichment tests.

- 503/backoff tests.

> **Izlazni artefakt sprinta**  
> Bandcamp URL import i stabilan metadata enrichment bez lažnih account mogućnosti.

## Completion report

Report changed files, validation commands and results, migrations, security/license impact, known risks and every unmet acceptance criterion.
