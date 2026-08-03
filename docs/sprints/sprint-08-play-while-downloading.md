## Sprint 8 - Play while downloading

## Status

Planned

## Required context

Always read `/AGENTS.md` and `/docs/project-state.yaml`, then:

- [PLAYER.md](../PLAYER.md)
- [API.md](../API.md)

## Scope rule

Do not implement future sprint scope. Stop and request an ADR if a locked decision must change.

> **Cilj sprinta**  
> Omogućiti kontroliranu reprodukciju djelomično preuzete Soulseek datoteke kada format i buffer to dopuštaju.

Ovisnosti: Sprintovi 5 i 7.

### Isporučivi rezultati

- ProgressiveMediaSource i buffer state.

- Codec capability matrix s feature flagom.

- Buffering UI i seek ograničenja.

- Fallback na playback nakon kompletnog downloada.

### Implementacijski zadaci

1. Povezati download progress s procjenom playable buffera.

1. Testirati growing-file ponašanje odabranog media enginea.

1. Implementirati početni buffer threshold.

1. Implementirati underrun i resume state.

1. Ograničiti seek na buffered range.

1. Obraditi candidate switch, cancel i failed incomplete file.

### Acceptance kriteriji

- Podržani MP3 fixture počinje svirati prije završetka downloada.

- Prespor download ulazi u Buffering i nastavlja bez corruptanja queuea.

- Ne podržani format čeka complete.

- Cancel zaustavlja playback i čisti privremeni source.

- Nema indeksiranja incomplete filea kao konačne library stavke.

### Obavezni testovi

- Controlled slow-stream tests.

- Underrun/resume tests.

- Cancel/switch candidate tests.

- Codec-specific regression tests.

> **Izlazni artefakt sprinta**  
> Eksperimentalni, feature-flagged “Play while downloading” s jasnim fallbackom.

## Completion report

Report changed files, validation commands and results, migrations, security/license impact, known risks and every unmet acceptance criterion.
