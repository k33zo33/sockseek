# Domain model

## 7. Domenski model

### 7.1. Glavni agregati

| Agregat | Svrha | Najvažnija pravila |
| --- | --- | --- |
| ExternalAccount | Povezani provider račun. | Token nije dio entiteta; čuva samo SecretReference i stanje autorizacije. |
| ExternalPlaylist | Providerova playlista ili javni URL snapshot. | Jedinstvena po Provider + ExternalPlaylistId + AccountId. |
| Playlist | Lokalna korisnička playlista. | Može biti Copy ili Mirror import; nikad ne ovisi o dostupnosti providera za playback. |
| PlaylistItem | Jedna željena pjesma u playlisti. | Čuva originalni provider item ID, poziciju, snapshot metapodataka i resolution status. |
| CanonicalTrack | Interni identitet pjesme. | Može imati više TrackSource i LocalMediaFile zapisa. |
| TrackSource | Veza kanonske pjesme s providerom ili MusicBrainzom. | Provider external ID je immutable identitet sourcea. |
| LocalMediaFile | Fizička audio datoteka. | Jedinstvena po normaliziranoj putanji; hash je opcionalan i računa se u pozadini. |
| ResolutionAttempt | Povijest pokušaja spajanja stavke. | Čuva score, metodu i korisnikovu odluku. |
| DownloadWorkflow | Application pogled na Core workflow. | Ne duplicira svaki Core detalj; čuva mapu na engine ID i trajne rezultate. |
| PlaybackQueue | Trajni red reprodukcije. | Stavka može biti LocalFile, ProgressiveDownload ili PendingResolution. |

### 7.2. Playlist item state machine

```text
Imported
   │
   ├─ local exact match ───────────────► AvailableLocal
   │
   ├─ probable match ─────────────────► ReviewRequired
   │                                      │ approve
   │                                      ▼
   │                                  AvailableLocal
   │
   └─ no local match ─────────────────► Unresolved
                                          │ resolve/download
                                          ▼
                                      Searching
                                          │ candidate
                                          ▼
                                      CandidateFound
                                          │ download
                                          ▼
                                      Downloading
                                          │ success
                                          ▼
                                      AvailableLocal

Terminal side states: Failed, Skipped, RemovedFromSourcePlaylist
```

### 7.3. Kanonsko spajanje pjesama

TrackIdentityService mora koristiti determinističke signale prije fuzzy usporedbe. Automatski spoj niske sigurnosti nije dopušten.

| Signal | Predloženi score | Pravilo |
| --- | --- | --- |
| Jednaki ISRC | 1.00 | Automatski spoj, osim ako trajanje odstupa više od 10 sekundi. |
| Jednaki MusicBrainz Recording MBID | 0.99 | Automatski spoj. |
| Prethodna korisnička odluka / source mapping | 1.00 | Uvijek koristiti dok source postoji. |
| Normalizirani artist + title + duration | 0.45 + 0.40 + 0.15 | Automatski samo ako ukupno >= 0.92. |
| Artist + title bez trajanja | najviše 0.88 | Zahtijeva review ako nema drugih signala. |
| Live/remix/edit/version konflikt | -0.20 do -0.40 | Smanjiti score; ne spajati studijsku i live verziju automatski. |
| Album podudaranje | +0.05 | Samo pomoćni signal, nikad dovoljan samostalno. |

> **Pragovi**  
> AutoMatchThreshold = 0.92; ReviewThreshold = 0.75. Vrijednosti moraju biti konfigurabilne i pokrivene testovima s fixture skupom različitih verzija, remixa, live snimki i feat. zapisa.
