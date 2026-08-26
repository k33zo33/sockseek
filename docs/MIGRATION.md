# Migration strategy from the existing fork

## 19. Strategija migracije iz postojećeg forka

### 19.1. Faze

| Faza | Opis | Rizik koji se izbjegava |
| --- | --- | --- |
| A - Freeze baseline | Tag, upstream sync, CI green, dokumentirani API snapshot. | Razvoj na zastarjeloj ili neponovljivoj osnovi. |
| B - Gateway | Novi Application sloj poziva postojeći daemon/Core kroz adapter. | Direktno vezanje UI-ja uz mutable Job objekte. |
| C - Persistence | Dodavanje SQLite modela bez mijenjanja engine configa. | Velika migracija više sustava odjednom. |
| D - UI vertical slice | Search -> candidate -> download -> local playback. | Godinama građen UI bez funkcionalnog end-to-end toka. |
| E - Provider imports | Spotify/YouTube/Bandcamp/MusicBrainz jedan po jedan. | Zajednički provider mega-abstraction prije stvarnih potreba. |
| F - Hardening | Security, packaging, legal i compliance. | Javni release neprovjerenog klijenta. |

### 19.2. Backward compatibility

- Postojeće CLI naredbe i config opcije ostaju funkcionalne tijekom MVP razvoja.

- Legacy API endpointovi se ne uklanjaju; novi API dobiva /api/v1 prefix.

- Existing job DTO-i se ne koriste kao persistence model.

- Novi player i provider slojevi ne smiju se uvlačiti u Sockseek.Core.

- Breaking change zahtijeva BREAKING.md zapis, migration note i major/minor odluku prema projektu.
