# Desktop UI and UX

## 14. Desktop UI/UX specifikacija

### 14.1. Navigacija

```text
Sidebar
  Home
  Search
  Playlists
  Library
  Downloads
  Accounts
  Settings

Persistent bottom player
  artwork | title/artist | previous | play/pause | next
  progress | volume | queue | expanded player
```

### 14.2. Onboarding

- Welcome i kratko objašnjenje da aplikacija koristi lokalne datoteke/Soulseek, a vanjske servise samo za playliste.

- Odabir download direktorija i library root direktorija.

- Soulseek prijava ili konfiguracija postojećeg accounta; credential test.

- Odabir default quality profila.

- Opcionalno povezivanje Spotify/YouTube računa; Bandcamp prikazuje Import public URL, ne Connect.

- AGPL, privatnost i legal use potvrda bez zastrašujućeg wall-of-texta.

- Završni health check: daemon, baza, write permissions, Soulseek status i audio engine.

### 14.3. Playlist detail ekran

| Zona | Sadržaj |
| --- | --- |
| Header | Cover, naziv, provider badge, zadnji sync, broj stavki, Resolve, Download missing, Play available. |
| Filter bar | All, Available, Missing, Downloading, Review, Failed; search unutar playliste. |
| Track row | Pozicija, naslov/artist/album, source badge, duration, resolution status, quality, akcijski menu. |
| Bulk selection | Resolve, download, apply profile, retry, skip, remove local-only. |
| Review drawer | Local match i Soulseek kandidati s jasnim scoreom, formatom, bitrateom, userom i brzinom. |
| Progress | Ukupni resolved/downloaded/failed brojevi i aktivni workflow status. |

### 14.4. Obavezni UI stateovi

- Loading skeleton, empty, disconnected, unauthorized, rate-limited, partial success i retryable error.

- Backend starting/restarting banner bez blokiranja cijelog prozora.

- Provider quota/approval upozorenje s razumljivim tekstom.

- Soulseek offline status i čekanje reconnecta.

- Nema generičkog “Something went wrong” bez correlation ID-a i akcije za copy diagnostics.

- Svaka destructive akcija mora navesti briše li samo zapis ili i fizičku datoteku.

### 14.5. Accessibility i shortcuts

- Sve funkcije moraju biti dostupne tipkovnicom.

- Vidljiv focus state i minimalan kontrast prema WCAG AA gdje je primjenjivo.

- Space: play/pause kada fokus nije u inputu; Ctrl+L: global search; Ctrl+K: command palette; Ctrl+, Settings.

- Media keys moraju raditi neovisno o fokusu prozora.

- Screen-reader label za status ikone; status se ne prenosi samo bojom.
