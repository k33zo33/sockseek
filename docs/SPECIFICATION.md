SOCKSEEK UI

Tehnička specifikacija, arhitektura i plan implementacije za Codex

Local-first desktop player i Soulseek downloader s uvozom playlista iz vanjskih servisa

| Repozitorij | k33zo33/sockseek |
| --- | --- |
| Polazna grana / commit | master / ef36306c86046757d76d6c1158a48c7b2f58dc2c |
| Licencni put | Put A - GNU AGPL v3, otvoreni izvorni kod |
| Datum specifikacije | 23. lipnja 2026. |

Namjena dokumenta

Ovaj dokument je provedbena specifikacija. Codex ga treba koristiti kao izvor istine za razvoj aplikacije iz postojećeg forka, sprint po sprint, bez promjene zaključanih odluka bez novog ADR-a.

# Kontrola dokumenta

| Polje | Vrijednost |
| --- | --- |
| Verzija | 1.0 |
| Status | Odobrena početna specifikacija |
| Vlasnik proizvoda | Zoran Miladinović |
| Namijenjeno | Codex, razvojni suradnici i budući maintaineri |
| Jezik | Hrvatski; nazivi koda, API-ja i projekata ostaju na engleskom |
| Izvor istine | Ovaj dokument + ADR datoteke koje se iz njega kreiraju u repozitoriju |
| Promjene odluka | Isključivo kroz novi ADR i PR koji ažurira ovu specifikaciju |

> **Zaključane odluke**  
> 1) Vanjski servisi ne reproduciraju niti isporučuju audio. 2) Oni služe samo za uvoz/sinkronizaciju playlista i metapodataka. 3) Sav playback dolazi iz lokalnih datoteka ili Soulseek downloada. 4) Projekt ostaje AGPL v3. 5) Postojeći Sockseek engine se zadržava iza adaptera i ne prepisuje se od nule.

# Sadržaj

Glavna poglavlja dokumenta:

| 1. Izvršni sažetak | 15. Sigurnost, privatnost i operativna pravila |
| --- | --- |
| 2. Zaključane odluke proizvoda | 16. Konfiguracija i profili |
| 3. Licencni i distribucijski model - Put A | 17. Testna strategija |
| 4. Polazno stanje postojećeg forka | 18. CI/CD i packaging |
| 5. Ciljna arhitektura | 19. Strategija migracije iz postojećeg forka |
| 6. Komponente i odgovornosti | 20. Plan razvoja po sprintovima |
| 7. Domenski model | 21. Codex razvojni runbook |
| 8. Persistence i baza podataka | 22. Globalni Definition of Done |
| 9. Integracijski ugovori | 23. MVP i javni release gateovi |
| 10. Uvoz, sinkronizacija i rješavanje playlista | 24. Post-MVP backlog |
| 11. Integracija s postojećim Soulseek engineom | 25. Početni prompt za Codex |
| 12. Player arhitektura | 26. Traceability matrica |
| 13. API i događaji | 27. Izvori i referentni dokumenti |
| 14. Desktop UI/UX specifikacija | 28. Konačna implementacijska direktiva |

# 1. Izvršni sažetak

Cilj je pretvoriti postojeći Sockseek fork u pristupačnu desktop glazbenu aplikaciju koja izgleda i ponaša se kao moderan player, ali koristi postojeći Soulseek engine za pronalaženje i preuzimanje glazbe. Korisnik može povezati podržane račune ili unijeti javnu playlistu, uvesti popis pjesama, riješiti stavke prema lokalnoj biblioteci ili Soulseek rezultatima, preuzeti nedostajuće pjesme i reproducirati ih iz iste aplikacije.

Spotify i YouTube bit će izvori korisničkih playlista. Bandcamp će u prvoj javnoj verziji podržavati javne URL-ove jer službeni API nije opći fan-account API. MusicBrainz se koristi kao javni metadata provider, a ne kao izvor playlista; za MetaBrainz korisničke playliste može se naknadno dodati ListenBrainz. Nijedan od tih servisa ne smije biti audio source unutar našeg playera.

Tehnički pristup je evolucijski: postojeći projekti Sockseek.Core, Sockseek.Api, Sockseek.Server i Sockseek.Cli ostaju funkcionalni. Dodaju se novi domenski, aplikacijski, infrastrukturni, integracijski, player i desktop slojevi. UI se spaja na lokalni daemon preko postojećeg HTTP/SignalR temelja. Time se smanjuje rizik i čuva dokazana logika pretrage, rangiranja, downloada, retryja, cancellationa i workflowa.

> **Konačna definicija proizvoda**  
> Sockseek UI je local-first desktop glazbeni player i downloader. Vanjske playliste predstavljaju željeni popis pjesama; aplikacija ih povezuje s lokalnim datotekama ili Soulseek rezultatima. Reprodukcija se obavlja isključivo iz lokalne datoteke, dovršenog downloada ili djelomično preuzete Soulseek datoteke kroz kontrolirani “Play while downloading” način.

## 1.1. Ključni ishodi MVP-a

- Moderan cross-platform desktop UI s onboardingom, pretragom, playlistama, bibliotekom, download queueom i playerom.

- Povezivanje Spotify i YouTube računa samo radi čitanja playlista i njihovih stavki.

- Import javnih Bandcamp URL-ova bez autentificiranog scrapinga.

- MusicBrainz metadata enrichment i identifikacija pjesama preko MBID/ISRC vrijednosti.

- Ujedinjena lokalna playlist struktura koja čuva izvorni provider i status svake stavke.

- Soulseek search, candidate review, bulk download i retry preko postojećeg enginea.

- Lokalni player s queueom, shuffle/repeat kontrolama i podržanim audio formatima.

- Opcionalni “Play while downloading” za formate koji prolaze capability test.

- AGPL-compliant distribucija s vidljivom licencom i dostupnim izvornim kodom.

## 1.2. MVP nije

- Spotify, YouTube, Bandcamp ili drugi streaming player.

- Alat za preuzimanje audija s YouTubea ili Spotifyja.

- Centralizirani cloud Soulseek servis za velik broj korisnika.

- Mobilna aplikacija ili web SaaS u prvoj fazi.

- Potpuno nova implementacija Soulseek protokola.

- Automatski downloader svih stavki bez korisnikove jasne akcije ili konfigurirane politike.

# 2. Zaključane odluke proizvoda

| ID | Odluka | Posljedica za implementaciju |
| --- | --- | --- |
| D-01 | Vanjski servisi su playlist/metadata izvori, ne audio izvori. | Provider ugovori nemaju playback ni download metode. UI nikad ne nudi “Play from Spotify/YouTube”. |
| D-02 | Sav audio dolazi iz lokalne biblioteke ili Soulseeka. | MediaSourceResolver vraća LocalFile ili ProgressiveSoulseekFile. |
| D-03 | Licenca ostaje GNU AGPL v3. | Izvorni kod i pravne obavijesti moraju biti dostupni korisniku; izvedeni rad ostaje AGPL. |
| D-04 | Postojeći Sockseek engine ostaje osnova. | Novi slojevi komuniciraju s njim preko adaptera; nema big-bang rewritea. |
| D-05 | Aplikacija je local-first. | Soulseek credentials, provider tokeni, baza i glazba ostaju na korisnikovu računalu. |
| D-06 | Desktop UI je cross-platform. | Ciljani UI framework je Avalonia; Windows je prvi release target, Linux drugi, macOS nakon stabilizacije. |
| D-07 | Backend ostaje odvojen proces. | Desktop shell pokreće lokalni Sockseek daemon i komunicira preko localhost HTTP + SignalR. |
| D-08 | Provider integracije su capability-driven. | UI prikazuje samo službeno dostupne funkcije; Bandcamp “Connect account” nije lažno izložen. |
| D-09 | Legacy CLI ostaje podržan. | Nove promjene ne smiju pokvariti postojeći CLI i daemon workflow. |
| D-10 | Promjena odluke zahtijeva ADR. | Codex ne smije samostalno promijeniti framework, bazu, licencu ili provider scope. |

## 2.1. Funkcionalni opseg po provideru

| Provider | MVP mogućnost | Nije dopušteno / nije dostupno | Status |
| --- | --- | --- | --- |
| Spotify | OAuth PKCE, popis korisničkih playlista, čitanje stavki, opcionalno saved tracks kao sintetička playlista. | Nema Spotify playbacka, audio streama ni “download from Spotify”. Development mode ima ograničen broj korisnika. | Implementirati iza feature flaga i jasnog quota upozorenja. |
| YouTube | Google OAuth, popis playlista korisnika, čitanje video metapodataka i redoslijeda. | Nema iframe playera, audio extractiona, YouTube downloada ni offline cachea. | Implementirati kao playlist import. |
| Bandcamp | Import javnog album/track URL-a i metapodataka; kasnije import javno dostupne kolekcije ako postoji stabilan službeni put. | Nema općeg fan OAuth API-ja; nema authenticated scrapinga ni spremanja cookiesa. | MVP: public URL import. |
| MusicBrainz | Javni metadata lookup, MBID/ISRC enrichment, kanonski artist/release/recording podaci. | MusicBrainz nema korisničke playliste za import. | Metadata provider bez obveznog accounta. |
| ListenBrainz | Opcionalno u kasnijem sprintu: korisničke playliste i povijest kao MetaBrainz account integracija. | Nije zamjena za MusicBrainz metadata model. | Post-MVP ili dio MetaBrainz sprinta. |
| Soulseek | Search, album folder discovery, download, retry, candidate ranking, progressive local playback. | Nije centralizirani cloud servis; mora se napraviti compliance audit prije javnog releasea. | Glavni audio backend. |

## 2.2. Važna ograničenja dostupnosti

- Spotify desktop aplikacija mora koristiti Authorization Code with PKCE jer ne može sigurno čuvati client secret. Spotify development mode trenutno je ograničen na najviše pet allowlistanih korisnika; šira distribucija ovisi o Spotify odobrenju i quota modu [R1-R3].

- YouTube Data API može vratiti playliste vlasnika autentificiranog računa kroz autorizirani zahtjev s mine=true; desktop OAuth koristi system browser i lokalni redirect URI [R4-R5].

- Bandcamp službeni API namijenjen je labelima i merchandise fulfillment partnerima, pa obični fan account connect nije dio MVP-a [R8].

- MusicBrainz treba globalni limiter od najviše približno jednog zahtjeva u sekundi po IP-u i smislen User-Agent [R6-R7].

- Soulseek pravila navode da automatizirani klijenti bez punog skupa funkcija nisu dopušteni te toleriraju alternativne klijente s punim funkcijama; prije javnog releasea potreban je poseban compliance sprint [R9].

# 3. Licencni i distribucijski model - Put A

Projekt nastavlja koristiti postojeći Sockseek kod pod GNU Affero General Public License v3. To je svjesna odluka: aplikacija ostaje otvorenog izvornog koda, a izmjene servera, enginea, API-ja i UI-ja objavljuju se pod istom licencom. Ovaj dokument nije pravni savjet, ali implementacija mora poštovati praktične zahtjeve licence i zadržati postojeći LICENSE.

## 3.1. Obavezne implementacijske mjere

- Ne uklanjati postojeći LICENSE niti copyright obavijesti.

- Dodati ekran Settings > About > License s nazivom licence, tekstom bez jamstva i poveznicom na izvorni kod.

- Distribucijski paket mora sadržavati THIRD-PARTY-NOTICES i uputu gdje se preuzima Corresponding Source.

- Svaki javno dostupan daemon build mora korisniku jasno ponuditi izvorni kod točno te verzije.

- Datoteke koje su značajno izmijenjene trebaju imati jasnu povijest kroz Git i release notes; ne umetati lažne autore.

- Frontend, backend i packaging skripte smatraju se dijelom istog proizvoda i ostaju AGPL-kompatibilni.

- Automatski generirani OpenAPI i migracije moraju biti u repozitoriju.

> **Release gate**  
> Nijedan javni binary release ne smije biti objavljen dok About ekran, LICENSE, THIRD-PARTY-NOTICES, source URL i release source tag nisu prisutni i testirani.

# 4. Polazno stanje postojećeg forka

Polazna točka je javni fork k33zo33/sockseek, grana master, commit ef36306c86046757d76d6c1158a48c7b2f58dc2c. U trenutku izrade dokumenta upstream fiso64/sockseek sadrži 25 dodatnih commitova. Fork nema vlastitu divergenciju koju treba čuvati, pa je preporučeno prvo napraviti kontrolirani fast-forward na provjereni upstream commit.

## 4.1. Dijelovi koje zadržavamo

| Projekt / komponenta | Vrijednost za novu aplikaciju | Strategija |
| --- | --- | --- |
| Sockseek.Core | DownloadEngine, job model, extractori, pretraga, rangiranje, downloader, skip logika, organizacija datoteka. | Zadržati i omotati adapterom; refaktorirati samo uz testove. |
| Sockseek.Api | Postojeći DTO-i i SockseekApiClient. | Proširiti verzioniranim app ugovorima; ne lomiti postojeći protokol. |
| Sockseek.Server | ASP.NET Core daemon, EngineSupervisor, state store, OpenAPI i SignalR. | Pretvoriti u lokalni application host; zadržati legacy endpointove. |
| Sockseek.Cli | Dokaz da lokalni i remote backend rade; napredni korisnici. | Održavati kompatibilnost i koristiti za dijagnostiku. |
| Test projekti | Velika postojeća pokrivenost core, CLI i server ponašanja. | Svi testovi moraju ostati zeleni nakon svakog sprinta. |
| Benchmark projekt | Mjerenje sortera, projekcija i velikih rezultata. | Proširiti za playlist resolution i library scan. |

## 4.2. Uočeni tehnički dug koji ne blokira prvi UI

- DownloadEngine je prevelik orkestrator i sam kod ga označava kao God Class. Ne prepisivati ga u početnim sprintovima; izolirati ga gatewayem.

- Job objekti su mutable i šalju PropertyChanged s background threadova. UI ne smije dobivati direktne Job instance; smije dobivati samo immutable snapshot DTO-e.

- M3uEditor upravlja i indeksom i playlistom. Razdvajanje je kasniji refactor, nakon funkcionalnog playera.

- Docker ostaje sekundarni headless packaging put. Sprint 0 je uskladio Dockerfile s `net10.0`, ali daemon/compose workflow i dalje treba zaseban pregled prije nego što se tretira kao polished deployment put.

- Daemon u baselineu nema dovoljno lokalne aplikacijske autentikacije. Desktop izdanje mora uvesti localhost session token i default bind samo na loopback.

## 4.3. Obavezni početni Git postupak

```bash
git checkout master
git pull --ff-only origin master
git tag sockseek-ui-baseline-ef36306 ef36306c86046757d76d6c1158a48c7b2f58dc2c
git remote add upstream https://github.com/fiso64/sockseek.git
git fetch upstream
git checkout -b codex/sprint-00-baseline-sync
git merge --ff-only upstream/master

dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build
```

## 4.4. Baseline sync gate

Prije prvog funkcionalnog sprinta Codex mora zaključati reproducibilnu početnu točku. Ovaj gate sprječava da se UI i novi application sloj grade nad nepoznatom ili djelomično sinkroniziranom verzijom enginea.

- Working tree mora biti čist, bez lokalnih necommitiranih izmjena.

- Remote origin mora pokazivati na k33zo33/sockseek, a upstream na fiso64/sockseek.

- Tag sockseek-ui-baseline-ef36306 mora pokazivati na dokumentirani baseline commit ef36306c86046757d76d6c1158a48c7b2f58dc2c.

- Codex mora dohvatiti upstream i izraditi compare izvještaj prije bilo kakvog mergea.

- Fast-forward je dopušten samo ako nema vlastite divergencije i nakon pregleda breaking promjena.

- Nakon sinkronizacije obavezni su dotnet restore, Release build i cijeli test suite.

- Konačni commit koji postaje razvojna baza mora se zapisati u docs/baseline.md i u prvi sprint PR.

> **STOP UVJET:** ako fast-forward više nije moguć, ako testovi baselinea ne prolaze ili se pojavi licencna/kompatibilnosna nejasnoća, Codex mora stati, dokumentirati razliku i otvoriti ADR/PR za odluku. Ne smije koristiti force-push niti silom prepisati master.

# 5. Ciljna arhitektura

## 5.1. Arhitekturna načela

- Local-first: glazba, credentials, provider tokeni i baza ostaju lokalni.

- API-first: Desktop UI komunicira s daemonom kroz verzionirani HTTP API i SignalR evente.

- Strangler pattern: novi slojevi postupno obavijaju postojeći Sockseek engine.

- Capability-driven providers: svaki provider deklarira što službeno podržava.

- Immutable UI state: UI dobiva DTO snapshotove, nikad mutable Core objekte.

- Idempotent sync: ponovljeni import iste playliste ne duplicira stavke.

- Explicit user action: download i spajanje nejasnih rezultata moraju biti vidljivi i poništivi.

- Secure by default: localhost only, PKCE, secret store, log redaction.

- Backward compatibility: CLI i postojeći daemon API ostaju funkcionalni do eksplicitne deprecacije.

## 5.2. Procesna topologija

```text
┌──────────────────────────────────────────────┐
│ Sockseek.Desktop (Avalonia UI)               │
│ - Views / ViewModels                         │
│ - Design system                              │
│ - Local daemon process supervisor            │
└──────────────────────┬───────────────────────┘
                       │ HTTP + SignalR, localhost session token
┌──────────────────────▼───────────────────────┐
│ Sockseek.Server / Local Application Host     │
│ - Existing job API                           │
│ - New /api/v1 application API                │
│ - Playlist sync and resolution               │
│ - Library index                              │
│ - Player coordinator                         │
│ - SQLite + secret store                      │
└───────────────┬────────────────┬─────────────┘
                │                │
      ┌─────────▼────────┐  ┌────▼────────────────────┐
      │ Existing Core    │  │ Provider integrations   │
      │ Soulseek engine  │  │ Spotify / YouTube       │
      │ search/download  │  │ Bandcamp / MusicBrainz  │
      └─────────┬────────┘  └─────────────────────────┘
                │
          Soulseek network
```

## 5.3. Zašto odvojeni daemon proces

- UI crash ne prekida nužno aktivne downloade.

- Postojeći remote CLI i budući remote-control klijent mogu koristiti isti API.

- OAuth callback, library scan i player ostaju u kontroliranom backend procesu.

- Moguće je pokretati daemon bez UI-ja za napredne korisnike.

- Engine logovi i crash recovery mogu se odvojiti od UI render loopa.

## 5.4. Ciljana struktura solutiona

```text
Existing projects (ostaju):
  Sockseek.Core
  Sockseek.Api
  Sockseek.Server
  Sockseek.Cli
  Sockseek.*.Tests
  Sockseek.Benchmarks

New projects:
  Sockseek.Domain
  Sockseek.Application
  Sockseek.Infrastructure
  Sockseek.Integrations.Abstractions
  Sockseek.Integrations.Spotify
  Sockseek.Integrations.YouTube
  Sockseek.Integrations.Bandcamp
  Sockseek.Integrations.MetaBrainz
  Sockseek.Player
  Sockseek.Desktop

New test projects:
  Sockseek.Domain.Tests
  Sockseek.Application.Tests
  Sockseek.Infrastructure.Tests
  Sockseek.Integrations.Tests
  Sockseek.Player.Tests
  Sockseek.Desktop.Tests
  Sockseek.E2E.Tests
```

## 5.5. Pravila ovisnosti

| Projekt | Smije ovisiti o | Ne smije ovisiti o |
| --- | --- | --- |
| Domain | Samo BCL. | EF Core, Avalonia, Spotify SDK, Soulseek.Core, filesystem. |
| Application | Domain, Integrations.Abstractions. | Avalonia, konkretni provider SDK-i, konkretna baza. |
| Infrastructure | Application, Domain. | Desktop UI. |
| Integrations.* | Application, Domain, Abstractions. | Desktop UI, Soulseek.Core internals. |
| Player | Application, Domain. | Provider SDK-i; vanjski streaming servisi. |
| Server | Application, Infrastructure, Player, adapter prema Coreu. | Avalonia. |
| Desktop | Sockseek.Api contracts/client, UI toolkit. | Sockseek.Core, EF DbContext, provider SDK-i. |

# 6. Komponente i odgovornosti

| Komponenta | Odgovornost | Ključni izlazi |
| --- | --- | --- |
| SoulseekEngineGateway | Pretvara application zahtjeve u postojeće engine jobove i mapira evente u snapshotove. | SearchSession, DownloadSnapshot, WorkflowSnapshot. |
| PlaylistImportService | Dohvaća vanjsku playlistu, normalizira stavke i sprema idempotentni snapshot. | LocalPlaylist + PlaylistItem zapisi. |
| PlaylistSyncService | Uspoređuje provider snapshot s lokalnim stanjem i čuva resolution/download status. | Added/updated/removed diff. |
| TrackIdentityService | Povezuje provider stavke, MusicBrainz identitete i lokalne datoteke. | CanonicalTrack + MatchDecision. |
| PlaylistResolutionService | Za neriješene stavke pokreće local lookup ili Soulseek search workflow. | Resolved, ReviewRequired ili Unresolved status. |
| LibraryIndexer | Skenira direktorije, čita tagove i prati promjene datoteka. | LocalMediaFile i LibraryTrack zapisi. |
| PlaybackCoordinator | Upravlja queueom, aktivnim sourceom, media engineom i player eventima. | PlayerStateSnapshot. |
| ProgressivePlaybackCoordinator | Određuje kada je djelomična datoteka spremna za reprodukciju. | Buffer state i playable source. |
| ProviderConnectionService | OAuth start/callback/refresh/disconnect bez izlaganja tokena UI-ju. | ExternalAccount status. |
| SecretStore | Sprema provider i eventualne Soulseek tajne u OS credential store. | Opaque secret reference. |
| AppStateStore | SQLite persistence i EF Core migracije. | Trajni lokalni application state. |
| DesktopDaemonSupervisor | Pokreće child daemon, čita port/token i radi crash restart politiku. | Backend connection state. |

# 7. Domenski model

## 7.1. Glavni agregati

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

## 7.2. Playlist item state machine

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

## 7.3. Kanonsko spajanje pjesama

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

# 8. Persistence i baza podataka

Koristi se SQLite s EF Core migracijama. Baza je lokalna i pripada jednom korisničkom profilu aplikacije. Provider tokeni ne spremaju se u SQLite u čitljivom obliku.

## 8.1. Predložene tablice

| Tablica | Ključna polja / indeksi |
| --- | --- |
| AppProfile | Id, Name, CreatedAtUtc, Active; podrška za buduće odvojene lokalne profile. |
| ExternalAccount | Id, Provider, ExternalUserId, DisplayName, SecretReference, Status, LastAuthorizedAtUtc; unique Provider+ExternalUserId. |
| ExternalPlaylist | Id, AccountId nullable, Provider, ExternalId, Url, Name, SnapshotVersion, LastSyncedAtUtc; unique Provider+ExternalId+AccountId. |
| Playlist | Id, Name, ImportMode, ExternalPlaylistId nullable, CreatedAtUtc, UpdatedAtUtc. |
| PlaylistItem | Id, PlaylistId, Position, ProviderItemId, CanonicalTrackId nullable, Status, SnapshotJson, RemovedAtUtc; unique PlaylistId+ProviderItemId. |
| Artist | Id, Name, SortName, MusicBrainzArtistId nullable. |
| Album | Id, Title, MusicBrainzReleaseGroupId nullable, Year nullable. |
| CanonicalTrack | Id, Title, DurationMs, Isrc nullable, MusicBrainzRecordingId nullable, NormalizedArtist, NormalizedTitle. |
| TrackSource | Id, CanonicalTrackId, Provider, ExternalId, ExternalUrl, RawMetadataJson; unique Provider+ExternalId. |
| LocalMediaFile | Id, CanonicalTrackId nullable, Path, Size, LastWriteUtc, DurationMs, Codec, Bitrate, SampleRate, BitDepth, Availability; unique normalized Path. |
| ResolutionAttempt | Id, PlaylistItemId, CandidateTrackId nullable, EngineJobId nullable, Method, Score, Decision, CreatedAtUtc. |
| DownloadRecord | Id, WorkflowId, EngineJobId, PlaylistItemId nullable, Status, OutputPath, CandidateJson, ErrorCode, timestamps. |
| PlaybackQueue | Id, Name, CurrentIndex, RepeatMode, ShuffleSeed, UpdatedAtUtc. |
| PlaybackQueueItem | Id, QueueId, Position, CanonicalTrackId, LocalMediaFileId nullable, DownloadRecordId nullable, State. |
| ProviderSyncState | Provider, AccountId, ResourceId, Cursor, ETag, LastSuccessUtc, LastError. |
| AppSetting | Key, JsonValue, UpdatedAtUtc. |
| SchemaInfo | ApplicationVersion, MigrationVersion, LastBackupUtc. |

## 8.2. Migracijska pravila

- Svaka schema promjena mora imati EF Core migraciju i test nadogradnje s prethodne release baze.

- Prije destructive migracije automatski napraviti kopiju baze u backup direktorij.

- Migracije se izvršavaju u daemonu prije prihvaćanja UI konekcije.

- Downgrade nije podržan; rollback releasea mora vratiti backup baze.

- Raw provider metadata može se čuvati kao JSON radi kompatibilnosti, ali ključna polja moraju biti normalizirana u stupce.

- Datoteke i glazba nikad se ne brišu samo zato što je provider stavka uklonjena iz source playliste.

# 9. Integracijski ugovori

## 9.1. Provider capabilities

```csharp
[Flags]
public enum PlaylistProviderCapabilities
{
    None = 0,
    ConnectAccount = 1 << 0,
    ImportPublicUrl = 1 << 1,
    ListUserPlaylists = 1 << 2,
    ReadPlaylistItems = 1 << 3,
    ReadSavedTracks = 1 << 4,
    IncrementalSync = 1 << 5,
    RequiresManualAppApproval = 1 << 6
}
```

## 9.2. Minimalni provider interface

```csharp
public interface IPlaylistSourceProvider
{
    string ProviderId { get; }
    PlaylistProviderCapabilities Capabilities { get; }

    Task<AuthorizationStartResult> StartAuthorizationAsync(
        AuthorizationRequest request,
        CancellationToken cancellationToken);

    Task<ExternalAccountSnapshot> CompleteAuthorizationAsync(
        AuthorizationCallback callback,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ExternalPlaylistSummary>> GetPlaylistsAsync(
        ExternalAccountId accountId,
        CancellationToken cancellationToken);

    Task<ExternalPlaylistSnapshot> GetPlaylistAsync(
        ExternalPlaylistRequest request,
        CancellationToken cancellationToken);

    Task DisconnectAsync(
        ExternalAccountId accountId,
        CancellationToken cancellationToken);
}
```

> **Zabranjeno u ugovoru**  
> Ne uvoditi IPlaybackProvider, GetAudioStreamAsync, DownloadTrackAsync niti provider audio URL. Vanjski servisi nisu media source.

## 9.3. Normalizirani vanjski track DTO

```csharp
public sealed record ExternalTrackSnapshot(
    string Provider,
    string ExternalTrackId,
    string Title,
    IReadOnlyList<string> Artists,
    string? Album,
    int? DurationMs,
    string? Isrc,
    string? ExternalUrl,
    string? ArtworkUrl,
    string? MusicBrainzRecordingId,
    string RawMetadataJson);
```

## 9.4. OAuth i tajne

- Desktop OAuth koristi system browser, PKCE, state i loopback redirect.

- UI dobiva samo status povezivanja i javni account profil; access/refresh token nikad ne ide u UI proces.

- Tokeni se spremaju preko ISecretStore; SQLite sadrži samo SecretReference.

- Refresh se obavlja unutar provider adaptera uz per-account lock da se izbjegne paralelni refresh race.

- Disconnect prvo revokea token ako provider podržava revoke, zatim briše secret i označava account disconnected.

- Log filter mora prepoznati Authorization, access_token, refresh_token, code_verifier i client_secret.

# 10. Uvoz, sinkronizacija i rješavanje playlista

## 10.1. Načini importa

| Način | Ponašanje | Kada koristiti |
| --- | --- | --- |
| Copy | Jednokratni snapshot. Nakon importa lokalna playlista je neovisna o provideru. | Default za javne URL-ove i korisnike koji žele ručno uređivati. |
| Mirror | Source playlist ostaje autoritet za redoslijed i članstvo; lokalni resolution/download podaci se čuvaju. | Spotify/YouTube povezani račun uz uključenu sinkronizaciju. |
| Append | Novi provider itemi se dodaju, ali uklanjanja na provideru ne uklanjaju lokalne stavke. | Arhivske i “collect forever” playliste. |

## 10.2. Idempotentni sync algoritam

- Dohvati provider snapshot sa stabilnim playlist item ili track ID vrijednostima.

- Normaliziraj sve stavke u ExternalTrackSnapshot.

- Usporedi po ProviderItemId; ne uspoređuj samo po poziciji.

- Ažuriraj naziv, poziciju i metadata snapshot bez brisanja CanonicalTrack veze.

- Nove stavke dodaj kao Imported/Unresolved.

- U Mirror modu nestale stavke označi RemovedAtUtc; ne briši lokalnu datoteku niti download record.

- Promjenu pozicije odradi u jednoj transakciji s privremenim sort keyem da se izbjegnu unique konflikti.

- Spremi provider cursor/ETag ako je dostupan.

- Emitiraj PlaylistSyncCompleted event s diff brojevima.

## 10.3. Resolution pipeline

```text
PlaylistItem
   │
   ├─ Existing manual mapping?
   │       └─ yes -> CanonicalTrack
   ├─ ISRC / MusicBrainz exact match?
   │       └─ yes -> CanonicalTrack
   ├─ Local library deterministic/fuzzy match?
   │       ├─ >= Auto threshold -> AvailableLocal
   │       └─ review range -> ReviewRequired
   └─ unresolved
           │ user presses Resolve / Download / Play
           ▼
      SoulseekEngineGateway search
           │
           ├─ auto profile selects candidate -> Downloading
           ├─ ambiguous -> CandidateReview
           └─ no result -> Failed/Retryable
```

## 10.4. Bulk playlist operacije

- Resolve all unresolved items.

- Download all missing items.

- Download selected items.

- Retry failed items.

- Apply quality profile to selected items.

- Skip item without deleting it.

- Manually map to local file.

- Manually choose Soulseek candidate.

- Remove from local playlist without modifying provider unless posebna write-back funkcija bude kasnije odobrena.

# 11. Integracija s postojećim Soulseek engineom

## 11.1. Gateway contract

```csharp
public interface ISoulseekEngineGateway
{
    Task<SearchHandle> StartTrackSearchAsync(
        TrackSearchRequest request,
        CancellationToken cancellationToken);

    Task<SearchHandle> StartAlbumSearchAsync(
        AlbumSearchRequest request,
        CancellationToken cancellationToken);

    Task<DownloadHandle> StartDownloadAsync(
        CandidateReference candidate,
        DownloadOptions options,
        CancellationToken cancellationToken);

    Task CancelJobAsync(Guid engineJobId, CancellationToken cancellationToken);
    Task<bool> TryNextCandidateAsync(Guid engineJobId, CancellationToken cancellationToken);
    Task<JobSnapshot?> GetJobAsync(Guid engineJobId, CancellationToken cancellationToken);

    IAsyncEnumerable<EngineEventEnvelope> SubscribeAsync(
        Guid workflowId,
        CancellationToken cancellationToken);
}
```

## 11.2. Adapter pravila

- Adapter je jedino novo mjesto koje smije poznavati Sockseek.Core Job tipove.

- Mapiranje je jednosmjerno: Core Job -> immutable DTO snapshot. UI nikad ne mutira Core Job.

- Postojeći EngineSupervisor i SockseekApiClient koriste se gdje je moguće umjesto direktnog pozivanja privatnih metoda.

- Novi application workflow ID mora se mapirati na postojeći Core WorkflowId.

- Engine DisplayId je prikazni podatak, ne trajni ključ u bazi.

- Core evente koalescirati prije slanja UI-ju kako progress eventovi ne bi preplavili render thread.

- Cancellation i next-candidate moraju ostati kompatibilni s postojećim CLI ponašanjem.

## 11.3. Što se ne refaktorira u prvoj fazi

- Unutarnji Searcher i Downloader algoritmi.

- ResultSorter kriteriji, osim dodatnih testova i bugfixeva.

- Existing extractor registry, osim uklanjanja provider playback/download koncepata iz novog UI-ja.

- Job state model, sve dok DTO adapter daje konzistentne snapshotove.

- Legacy CLI configuration binding.

# 12. Player arhitektura

Player je novi application subsystem. Ne koristi Spotify, YouTube, Bandcamp ni drugi provider kao audio source. MediaSourceResolver vraća samo lokalnu datoteku ili datoteku koju trenutno piše Soulseek downloader.

## 12.1. Player komponente

| Komponenta | Odgovornost |
| --- | --- |
| PlaybackCoordinator | Jedini vlasnik player state machinea, queuea i komandi. |
| IMediaEngine | Apstrakcija nad LibVLCSharp ili drugim odabranim lokalnim media engineom. |
| MediaSourceResolver | Odabire najbolju lokalnu datoteku ili pokreće resolve/download workflow. |
| PlaybackQueueStore | Trajno čuva queue, current index, shuffle seed i repeat mode. |
| ProgressivePlaybackCoordinator | Provjerava capability, buffer i rast datoteke. |
| MediaMetadataReader | Čita trajanje, codec, tags, ReplayGain i cover art iz lokalne datoteke. |
| SystemMediaSessionBridge | Media keys, OS now-playing metadata i headset kontrole. |

## 12.2. Player state machine

```text
Stopped
   │ Play
   ▼
ResolvingSource ── unresolved ─► Searching/Downloading
   │ local file                     │ buffer ready / complete
   ▼                                ▼
Loading ── ready ─► Playing ◄──── Buffering
  │                 │  │             ▲
  │ error           │  └ Pause       │ underrun
  ▼                 ▼                │
Failed            Paused ── Play ────┘

Playing ── end ─► Next queue item / Completed
```

## 12.3. MVP player funkcije

- Play, pause, stop, next, previous.

- Seek unutar dostupnog raspona.

- Volume i mute.

- Queue add/remove/reorder/clear.

- Repeat none/one/all i deterministic shuffle.

- Media keys i osnovni OS now-playing metadata.

- Resume posljednjeg queuea nakon restarta.

- Podrška za MP3, FLAC, Ogg/Vorbis, Opus, WAV, AAC/M4A prema capability testu media enginea.

- Error state po stavci bez rušenja cijelog playera.

## 12.4. Play while downloading

Ova funkcija nije mrežni streaming servis. Downloader i dalje stvara lokalnu .incomplete datoteku; player je otvara tek kada ima dovoljno podataka. Funkcija mora biti eksperimentalna dok svaki codec ne prođe testove.

- Capability matrica određuje podržava li format čitanje rastuće datoteke.

- Minimalni početni buffer računa se kao max(configured seconds × procijenjeni bitrate, minimal bytes).

- Player smije seekati samo unutar buffered rangea.

- Ako download speed padne ispod playback ratea, stanje prelazi u Buffering.

- Promjena kandidata zatvara media source, čisti stari buffer i ponovno ulazi u ResolvingSource.

- Ako format nije progressive-safe, playback počinje tek nakon potpunog downloada.

- Nedovršena datoteka se ne indeksira kao trajna library datoteka.

# 13. API i događaji

Postojeći /api/jobs i /api/workflows endpointovi ostaju radi kompatibilnosti. Novi UI koristi verzionirani application API pod /api/v1. Backend se po defaultu veže samo na 127.0.0.1 i zahtijeva session token koji desktop proces dobiva pri pokretanju daemona.

## 13.1. Glavni endpointi

| Područje | Endpointi |
| --- | --- |
| System | • GET /api/v1/system/info<br>• GET /api/v1/system/health<br>• GET /api/v1/system/capabilities<br>• POST /api/v1/system/shutdown |
| Onboarding | • GET /api/v1/onboarding/state<br>• POST /api/v1/onboarding/complete<br>• POST /api/v1/onboarding/test-soulseek |
| Accounts | • GET /api/v1/accounts<br>• POST /api/v1/accounts/{provider}/authorize<br>• GET /api/v1/accounts/{provider}/callback<br>• DELETE /api/v1/accounts/{id} |
| Providers | • GET /api/v1/providers<br>• GET /api/v1/providers/{provider}/capabilities |
| Playlists | • GET /api/v1/playlists<br>• POST /api/v1/playlists/import<br>• POST /api/v1/playlists/{id}/sync<br>• POST /api/v1/playlists/{id}/resolve<br>• POST /api/v1/playlists/{id}/download |
| Playlist items | • PATCH /api/v1/playlist-items/{id}<br>• POST /api/v1/playlist-items/{id}/resolve<br>• POST /api/v1/playlist-items/{id}/choose-match<br>• POST /api/v1/playlist-items/{id}/download |
| Library | • GET /api/v1/library/tracks<br>• GET /api/v1/library/albums<br>• POST /api/v1/library/roots<br>• POST /api/v1/library/scan<br>• POST /api/v1/library/tracks/{id}/relink |
| Downloads | • GET /api/v1/downloads<br>• POST /api/v1/downloads/{id}/cancel<br>• POST /api/v1/downloads/{id}/next-candidate<br>• POST /api/v1/downloads/{id}/retry |
| Player | • GET /api/v1/player/state<br>• GET /api/v1/player/queue<br>• POST /api/v1/player/play<br>• POST /api/v1/player/pause<br>• POST /api/v1/player/seek<br>• POST /api/v1/player/queue/items |
| Settings | • GET /api/v1/settings<br>• PATCH /api/v1/settings<br>• GET /api/v1/profiles |

## 13.2. SignalR događaji

```text
system.backend-state-changed
provider.authorization-changed
provider.sync-progress
playlist.sync-completed
playlist.item-state-changed
playlist.resolution-progress
library.scan-progress
library.file-changed
search.results-updated
download.state-changed
download.progress
player.state-changed
player.position-changed
player.buffer-changed
notification.created
```

Svaki event envelope mora sadržavati EventId, EventType, OccurredAtUtc, CorrelationId, WorkflowId nullable, EntityId nullable, Sequence i Payload. Klijent mora moći nakon reconnecta dohvatiti autoritativni snapshot; eventovi nisu jedini source of truth.

# 14. Desktop UI/UX specifikacija

## 14.1. Navigacija

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

## 14.2. Onboarding

- Welcome i kratko objašnjenje da aplikacija koristi lokalne datoteke/Soulseek, a vanjske servise samo za playliste.

- Odabir download direktorija i library root direktorija.

- Soulseek prijava ili konfiguracija postojećeg accounta; credential test.

- Odabir default quality profila.

- Opcionalno povezivanje Spotify/YouTube računa; Bandcamp prikazuje Import public URL, ne Connect.

- AGPL, privatnost i legal use potvrda bez zastrašujućeg wall-of-texta.

- Završni health check: daemon, baza, write permissions, Soulseek status i audio engine.

## 14.3. Playlist detail ekran

| Zona | Sadržaj |
| --- | --- |
| Header | Cover, naziv, provider badge, zadnji sync, broj stavki, Resolve, Download missing, Play available. |
| Filter bar | All, Available, Missing, Downloading, Review, Failed; search unutar playliste. |
| Track row | Pozicija, naslov/artist/album, source badge, duration, resolution status, quality, akcijski menu. |
| Bulk selection | Resolve, download, apply profile, retry, skip, remove local-only. |
| Review drawer | Local match i Soulseek kandidati s jasnim scoreom, formatom, bitrateom, userom i brzinom. |
| Progress | Ukupni resolved/downloaded/failed brojevi i aktivni workflow status. |

## 14.4. Obavezni UI stateovi

- Loading skeleton, empty, disconnected, unauthorized, rate-limited, partial success i retryable error.

- Backend starting/restarting banner bez blokiranja cijelog prozora.

- Provider quota/approval upozorenje s razumljivim tekstom.

- Soulseek offline status i čekanje reconnecta.

- Nema generičkog “Something went wrong” bez correlation ID-a i akcije za copy diagnostics.

- Svaka destructive akcija mora navesti briše li samo zapis ili i fizičku datoteku.

## 14.5. Accessibility i shortcuts

- Sve funkcije moraju biti dostupne tipkovnicom.

- Vidljiv focus state i minimalan kontrast prema WCAG AA gdje je primjenjivo.

- Space: play/pause kada fokus nije u inputu; Ctrl+L: global search; Ctrl+K: command palette; Ctrl+, Settings.

- Media keys moraju raditi neovisno o fokusu prozora.

- Screen-reader label za status ikone; status se ne prenosi samo bojom.

# 15. Sigurnost, privatnost i operativna pravila

## 15.1. Lokalni API

- Bind default isključivo na loopback.

- Daemon pri startu generira 256-bitni session token i zapisuje ga u file s user-only permissions ili siguran IPC handshake.

- Desktop šalje token u Authorization headeru; Swagger/OpenAPI UI nije izložen u release buildu osim ako je developer mode uključen.

- Remote bind je post-MVP i zahtijeva poseban authentication ADR.

- CORS nije univerzalno otvoren; dopušten je samo desktop origin/loopback model koji je stvarno potreban.

## 15.2. File-system sigurnost

- Remote Soulseek filename nikad ne postaje putanja bez sanitizacije i canonical path provjere.

- Output path mora ostati unutar konfiguriranog root direktorija.

- Symlink i junction traversal mora biti testiran na podržanim OS-ovima.

- Brisanje datoteke koristi recycle/trash kada je moguće ili eksplicitnu potvrdu za permanent delete.

- Library scan preskače system directories i ne slijedi symlinkove po defaultu.

## 15.3. Privatnost i logovi

- Telemetry je isključena po defaultu.

- Logovi ne smiju sadržavati provider tokene, OAuth code, Soulseek lozinku, puni Authorization header ni privatne playlist URL parametre.

- Diagnostics export mora imati redaction korak i korisniku prikazati što se izvozi.

- External provider raw metadata zadržava se samo koliko je potrebno za sync i troubleshooting.

- Disconnect account akcija briše secret i omogućuje brisanje provider snapshot podataka bez brisanja lokalne glazbe.

# 16. Konfiguracija i profili

Postojeći Sockseek config/profiles sustav ostaje izvor engine i download kvalitete. Nova baza čuva UI, library, account i player postavke. U prvoj fazi ne treba migrirati cijeli legacy config u SQLite.

| Vrsta postavke | Source of truth |
| --- | --- |
| Soulseek engine credentials i concurrency | Existing Sockseek config, kasnije ISecretStore + DB UI editor. |
| Download/search quality profiles | Existing profile catalog; UI ih dohvaća kroz API. |
| Download output paths | Existing DownloadSettings uz UI editor i validaciju. |
| Library roots | SQLite. |
| Provider accounts i sync | SQLite + ISecretStore. |
| Player preferences | SQLite. |
| UI theme/layout | Desktop local settings ili SQLite per profile. |

# 17. Testna strategija

| Razina | Obuhvat | Alati / pristup |
| --- | --- | --- |
| Unit | Domain score, state transitions, sync diff, queue logic, path safety. | MSTest postojeći standard ili jedan konsolidirani framework; bez mreže. |
| Integration | EF migracije, provider HTTP adapteri, daemon auth, player adapter. | Temp SQLite, fake HTTP server, fixture odgovori. |
| Contract | OpenAPI i provider DTO mapping. | Snapshot tests; backward compatibility za legacy API. |
| Core parity | Local/remote gateway rezultat isti kao postojeći CLI backend. | Existing mock Soulseek client i parity testovi. |
| UI component | ViewModel commands, loading/error state, list virtualizacija. | Avalonia headless tests. |
| E2E | Onboarding, import, resolve, download, playback, restart. | Packaged daemon + desktop test harness i mock providers. |
| Performance | 10k/100k library entries, velika playlista, veliki Soulseek result set. | BenchmarkDotNet i deterministic fixtures. |
| Security | Token redaction, traversal, auth, secret deletion. | Automated adversarial integration tests. |

## 17.1. Minimalna test matrica

- Windows 11 x64: obavezni MVP target.

- Ubuntu LTS x64: obavezni prije beta releasea.

- macOS arm64: build i smoke test prije označavanja cross-platform stable.

- SQLite upgrade iz svake javne release verzije.

- MP3, FLAC, Ogg, Opus, WAV, AAC/M4A player fixtures.

- Provider response fixtures za pagination, token expiry, 401, 403, 429 i malformed item.

- Soulseek disconnect tijekom searcha i downloada.

- Aplikacija restartana tijekom active download workflowa.

# 18. CI/CD i packaging

## 18.1. CI pipeline

```text
restore
  -> build Release
  -> unit tests
  -> integration tests
  -> architecture tests
  -> OpenAPI drift check
  -> UI build/headless tests
  -> dependency/license scan
  -> package smoke build
  -> artifacts
```

- Central Package Management za nove projekte; package versions se ne rasipaju po csproj datotekama.

- NuGet lock files ostaju uključeni.

- Generated files moraju se provjeravati za drift u CI-u.

- Release build je self-contained za ciljani RID.

- VLC/native media dependency pakira se po OS-u i evidentira u third-party notices.

- Docker nije glavni desktop distribution mehanizam; Dockerfile se ipak ažurira za headless daemon.

## 18.2. Desktop package

- Sockseek.Desktop executable i self-contained daemon executable.

- Installer kreira user-level data/config/log directories.

- Single-instance lock i deep-link/loopback OAuth callback handling.

- Auto-update nije uključen dok signature i rollback nisu definirani.

- Database backup prije updatea koji sadrži migraciju.

- About, license, source link i version/commit metadata.

# 19. Strategija migracije iz postojećeg forka

## 19.1. Faze

| Faza | Opis | Rizik koji se izbjegava |
| --- | --- | --- |
| A - Freeze baseline | Tag, upstream sync, CI green, dokumentirani API snapshot. | Razvoj na zastarjeloj ili neponovljivoj osnovi. |
| B - Gateway | Novi Application sloj poziva postojeći daemon/Core kroz adapter. | Direktno vezanje UI-ja uz mutable Job objekte. |
| C - Persistence | Dodavanje SQLite modela bez mijenjanja engine configa. | Velika migracija više sustava odjednom. |
| D - UI vertical slice | Search -> candidate -> download -> local playback. | Godinama građen UI bez funkcionalnog end-to-end toka. |
| E - Provider imports | Spotify/YouTube/Bandcamp/MusicBrainz jedan po jedan. | Zajednički provider mega-abstraction prije stvarnih potreba. |
| F - Hardening | Security, packaging, legal i compliance. | Javni release neprovjerenog klijenta. |

## 19.2. Backward compatibility

- Postojeće CLI naredbe i config opcije ostaju funkcionalne tijekom MVP razvoja.

- Legacy API endpointovi se ne uklanjaju; novi API dobiva /api/v1 prefix.

- Existing job DTO-i se ne koriste kao persistence model.

- Novi player i provider slojevi ne smiju se uvlačiti u Sockseek.Core.

- Breaking change zahtijeva BREAKING.md zapis, migration note i major/minor odluku prema projektu.

# 20. Plan razvoja po sprintovima

Plan pretpostavlja sprintove od približno dva tjedna, ali Codex ne smije vezati kvalitetu uz kalendarski rok. Svaki sprint mora završiti mergeabilnim vertikalnim rezultatom, zelenim testovima i dokumentacijom. Sprint se smije podijeliti u više PR-ova, ali ne smije spojiti više budućih sprintova u jedan nekontrolirani refactor.

| Sprint | Tema | MVP milestone |
| --- | --- | --- |
| 0 | Baseline, upstream i AGPL odluke | Reproducibilna osnova |
| 1 | Arhitekturni foundation | Novi slojevi i CI |
| 2 | Soulseek gateway i lokalni API security | Stabilna granica prema engineu |
| 3 | Domain i SQLite persistence | Trajni katalog/playlist model |
| 4 | Avalonia desktop shell | Pokretljiv UI + daemon |
| 5 | Soulseek search/download UI | Prvi end-to-end download |
| 6 | Lokalna library | Indeksirana glazba |
| 7 | Player MVP | Lokalna reprodukcija |
| 8 | Play while downloading | Progressive Soulseek playback |
| 9 | Provider framework i secret store | Sigurne integracije |
| 10 | Spotify playlist import | Prvi account provider |
| 11 | YouTube playlist import | Drugi account provider |
| 12 | Bandcamp + MetaBrainz | URL import i metadata |
| 13 | Unified playlist resolution | Cijela imported playlista -> player/downloader |
| 14 | Packaging, legal i security hardening | Release candidate |
| 15 | Performance, compliance i beta stabilizacija | Javna beta odluka |

## Sprint 0 - Baseline, upstream sync i AGPL setup

> **Cilj sprinta**  
> Stvoriti reproducibilnu, pravno i tehnički jasnu početnu točku prije funkcionalnih promjena.

Ovisnosti: Nema.

### Isporučivi rezultati

- Tag baseline commita i fast-forward forka na pregledani upstream.

- docs/adr/0001-agpl-product.md, 0002-local-first.md i 0003-provider-playlist-only.md.

- Ažurirani README s novom product vizijom bez obećanja vanjskog streaminga.

- CI baseline report i popis poznatih warninga.

### Implementacijski zadaci

1. Napraviti sigurnosnu kopiju grane i tag baselinea.

1. Sinkronizirati upstream u zasebnoj codex grani i pokrenuti puni test suite.

1. Provjeriti LICENSE, copyright i third-party licence.

1. Dodati docs/product-scope.md iz zaključanih odluka ovog dokumenta.

1. Dodati docs/provider-capability-matrix.md.

1. Ažurirati Docker issue jer baseline Dockerfile nije net10.0 kompatibilan.

### Acceptance kriteriji

- master nije force-pushan.

- dotnet build i svi postojeći testovi prolaze na sinkroniziranom baselineu.

- Repo sadrži tri ADR-a i product scope.

- Dokumentacija eksplicitno zabranjuje provider playback/download.

- AGPL source/link zahtjevi nalaze se u release checklisti.

### Obavezni testovi

- Puni postojeći test suite.

- CI run na Linuxu.

- Ručna provjera da legacy CLI help radi.

> **Izlazni artefakt sprinta**  
> PR koji samo stabilizira osnovu i dokumentira odluke; bez UI funkcionalnosti.

## Sprint 1 - Arhitekturni foundation

> **Cilj sprinta**  
> Dodati nove projekte i dependency granice bez promjene ponašanja postojećeg enginea.

Ovisnosti: Sprint 0.

### Isporučivi rezultati

- Novi Domain, Application, Infrastructure, Integrations.Abstractions, Player i Desktop skeleton projekti.

- Central package management i architecture tests.

- Osnovni /api/v1/system/info i /health endpointi.

- App correlation ID i structured error envelope.

### Implementacijski zadaci

1. Kreirati projekte i reference prema pravilima ovisnosti.

1. Dodati Result/Error tipove za application use caseove.

1. Uvesti IClock, IIdGenerator i IFileSystem apstrakcije gdje testabilnost to zahtijeva.

1. Dodati globalno exception mapiranje za novi API.

1. Dodati OpenAPI drift test.

1. Postaviti build za Desktop skeleton bez pokretanja enginea.

### Acceptance kriteriji

- Domain nema package reference na EF, Avalonia, provider SDK ili Sockseek.Core.

- Server vraća version, commit i capability snapshot.

- Architecture tests padaju ako Desktop referencira Core.

- Legacy API i CLI testovi ostaju zeleni.

### Obavezni testovi

- Architecture dependency tests.

- System endpoint integration test.

- OpenAPI snapshot test.

> **Izlazni artefakt sprinta**  
> Solution koja se gradi s novim praznim slojevima i dokazanim dependency pravilima.

## Sprint 2 - Soulseek gateway i lokalni API security

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

## Sprint 3 - Domain model i SQLite persistence

> **Cilj sprinta**  
> Uvesti trajni kanonski katalog, playliste, provider snapshotove, local media zapise i migracije.

Ovisnosti: Sprint 1; može se paralelno završavati sa Sprintom 2 uz odvojene PR-ove.

### Isporučivi rezultati

- Domain agregati i state enumovi.

- EF Core DbContext i početna SQLite migracija.

- Repository/use-case sloj za playliste i canonical tracks.

- Database backup/migration runner.

### Implementacijski zadaci

1. Implementirati tablice iz poglavlja 8.

1. Dodati unique indekse i concurrency tokene.

1. Implementirati idempotentno spremanje ExternalPlaylist snapshotova.

1. Implementirati TrackIdentityService s exact match metodama i konfigurabilnim fuzzy pragovima.

1. Dodati migration backup prije promjene schema verzije.

1. Dodati seed samo za development fixture podatke.

### Acceptance kriteriji

- Ponovljeni import istog snapshot-a ne duplicira podatke.

- Brisanje ExternalAccounta ne briše LocalMediaFile niti CanonicalTrack koji imaju druge veze.

- Migracija iz prazne i prethodne test baze prolazi.

- Token vrijednosti ne postoje u DB schema/modelu.

### Obavezni testovi

- SQLite repository integration tests.

- Migration upgrade/backup tests.

- Track matching fixture tests.

- Concurrency/idempotency tests.

> **Izlazni artefakt sprinta**  
> Trajna lokalna baza spremna za UI, provider import i player queue.

## Sprint 4 - Avalonia desktop shell i daemon supervisor

> **Cilj sprinta**  
> Napraviti user-friendly desktop okvir koji automatski pokreće lokalni daemon i prikazuje njegovo stanje.

Ovisnosti: Sprintovi 1-2.

### Isporučivi rezultati

- Avalonia app shell, sidebar, routing i design tokeni.

- DesktopDaemonSupervisor i secure session handshake.

- Home, Search, Playlists, Library, Downloads, Accounts i Settings prazne stranice.

- Persistent bottom player placeholder.

### Implementacijski zadaci

1. Implementirati single-instance desktop proces.

1. Pokrenuti self-contained ili development daemon child process.

1. Čitati port/token preko sigurnog startup handshaka.

1. Implementirati API client i SignalR reconnect manager.

1. Uvesti theme, localization-ready resources i command palette skeleton.

1. Implementirati backend starting/restarting/disconnected UX.

### Acceptance kriteriji

- Korisnik ne mora ručno pokretati daemon.

- UI se oporavlja nakon kontroliranog restarta daemona.

- Desktop nema referencu na Sockseek.Core ni DbContext.

- Sve glavne stranice i navigation shortcuts rade.

- Light/dark tema se pamti.

### Obavezni testovi

- ViewModel unit tests.

- Headless navigation tests.

- Daemon start/restart integration test.

- Session token handshake test.

> **Izlazni artefakt sprinta**  
> Instalabilni development shell povezan sa stvarnim lokalnim daemonom.

## Sprint 5 - Soulseek search i download UI

> **Cilj sprinta**  
> Isporučiti prvi puni vertikalni tok: search -> candidate -> download -> otvorena lokalna datoteka.

Ovisnosti: Sprintovi 2 i 4.

### Isporučivi rezultati

- Search ekran s track/album modom.

- Candidate list i album folder pregled.

- Download queue ekran i notifications.

- Cancel, retry i next-candidate akcije.

### Implementacijski zadaci

1. Mapirati quality profile i osnovne filtere u search request.

1. Virtualizirati veliki result list.

1. Prikazati user, slot, speed, format, bitrate, sample rate, bit depth i trajanje gdje postoje.

1. Dodati candidate review i explicit download action.

1. Prikazati workflow tree/detail drawer.

1. Dodati “Open file/folder” nakon uspjeha.

### Acceptance kriteriji

- Korisnik može pretražiti i preuzeti pojedinačnu pjesmu bez CLI-ja.

- Progress se ažurira bez blokiranja UI-ja.

- Cancel i next candidate mijenjaju stvarni engine posao.

- Greške imaju retry i correlation ID.

- Veliki result list ne zamrzava UI.

### Obavezni testovi

- Mock Soulseek E2E search/download.

- Large result virtualization test.

- Disconnect/reconnect tijekom downloada.

- UI error-state tests.

> **Izlazni artefakt sprinta**  
> Prva korisna desktop verzija koja zamjenjuje CLI za pojedinačni download.

## Sprint 6 - Lokalna glazbena biblioteka

> **Cilj sprinta**  
> Indeksirati postojeću glazbu i omogućiti da imported playlist prvo koristi lokalne datoteke.

Ovisnosti: Sprintovi 3-4.

### Isporučivi rezultati

- Library root management.

- Background scan i file watcher.

- Artist/album/track prikazi i lokalna pretraga.

- LocalMediaFile -> CanonicalTrack matching.

### Implementacijski zadaci

1. Implementirati TagLib metadata reader i codec/property extraction.

1. Dodati scan checkpoint i progress evente.

1. Detektirati deleted/moved/modified datoteke.

1. Uvesti optional content hash kao low-priority background posao.

1. Implementirati duplicate grouping.

1. Dodati manual relink i rescan akcije.

### Acceptance kriteriji

- Ponovljeni scan ne duplicira file zapise.

- Promijenjeni tagovi se osvježavaju.

- Obrisana datoteka postaje unavailable bez brisanja track identiteta.

- 10.000 track fixture se pretražuje i prikazuje bez zamrzavanja.

- Playlist item s exact local matchom automatski postaje AvailableLocal.

### Obavezni testovi

- Temp-directory scan integration tests.

- Tag fixture tests.

- Move/delete watcher tests.

- 10k performance benchmark.

> **Izlazni artefakt sprinta**  
> Funkcionalna lokalna biblioteka i source resolver za player.

## Sprint 7 - Lokalni player MVP

> **Cilj sprinta**  
> Reproducirati lokalne i dovršene Soulseek datoteke kroz stabilan player i trajni queue.

Ovisnosti: Sprint 6; Sprint 4 UI shell.

### Isporučivi rezultati

- IMediaEngine adapter i PlaybackCoordinator.

- Bottom player i expanded queue ekran.

- Trajni queue i media key podrška.

- Codec capability report.

### Implementacijski zadaci

1. Napraviti spike i odabrati LibVLCSharp ili drugi lokalni engine kroz ADR.

1. Implementirati player state machine.

1. Dodati queue persistence i deterministic shuffle.

1. Dodati seek, volume, repeat i error handling.

1. Dodati OS media session bridge po platformi.

1. Čitati cover art i now-playing metadata iz lokalne datoteke.

### Acceptance kriteriji

- MP3, FLAC, Ogg, Opus, WAV i M4A fixture matrica ima dokumentirani rezultat.

- Queue se vraća nakon restarta.

- Jedan neispravan file ne ruši cijeli player.

- Media keys rade na Windows targetu.

- Player nikad ne pokušava provider audio URL.

### Obavezni testovi

- Player state unit tests.

- Codec fixture integration tests.

- Queue persistence tests.

- Long playback smoke test.

> **Izlazni artefakt sprinta**  
> Aplikacija je pun lokalni player za library i dovršene downloade.

## Sprint 8 - Play while downloading

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

## Sprint 9 - Provider framework i secret store

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

## Sprint 10 - Spotify playlist import

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

## Sprint 11 - YouTube playlist import

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

## Sprint 12 - Bandcamp public URL i MetaBrainz metadata

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

## Sprint 13 - Unified playlist resolution i bulk workflow

> **Cilj sprinta**  
> Spojiti provider import, lokalnu biblioteku, Soulseek search, download i player u jedan user-friendly playlist tok.

Ovisnosti: Sprintovi 5-12.

### Isporučivi rezultati

- Playlist resolution orchestrator.

- Bulk resolve/download/play UI.

- Review queue za nejasne local/Soulseek matchove.

- Trajni workflow recovery nakon restarta.

### Implementacijski zadaci

1. Za svaku stavku prvo provjeriti manual mapping, exact IDs i local library.

1. Batchati Soulseek submissione uz postojeće concurrency limite.

1. Povezati application PlaylistItem status s Core workflow/job snapshotovima.

1. Implementirati bulk pause/cancel/retry bez rušenja uspješnih stavki.

1. Implementirati Play available i Play from here; unresolved stavka može pokrenuti resolve-and-play workflow.

1. Sačuvati korisničke candidate odluke za budući sync.

1. Dodati summary: available, downloading, review, failed i skipped.

### Acceptance kriteriji

- Spotify/YouTube/Bandcamp imported playlista može postati potpuno lokalno reproducibilna kroz biblioteku i Soulseek.

- Restart tijekom bulk downloada ne gubi trajne rezultate; aktivni engine posao se korektno rehidrira ili označi za retry.

- User review odluka ostaje nakon provider synca.

- Play nikad ne kontaktira provider audio servis.

- Partial success je jasno prikazan i moguće ga je nastaviti.

### Obavezni testovi

- End-to-end imported playlist fixtures.

- Restart/recovery tests.

- Bulk cancel/retry tests.

- Manual review persistence tests.

- Provider-to-local/Soulseek source resolver tests.

> **Izlazni artefakt sprinta**  
> MVP glavni proizvod: vanjska playlista -> lokalni player/downloader.

## Sprint 14 - Packaging, pravne obavijesti i security hardening

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

## Sprint 15 - Performance, Soulseek compliance i beta stabilizacija

> **Cilj sprinta**  
> Odlučiti je li aplikacija spremna za javnu beta distribuciju i ukloniti najveće operativne rizike.

Ovisnosti: Sprint 14.

### Isporučivi rezultati

- Performance report i optimizacije.

- Soulseek client feature/compliance audit.

- Public beta checklist i known limitations.

- Crash/diagnostic feedback proces.

### Implementacijski zadaci

1. Testirati 100k library zapisa, 10k playlistu i velike Soulseek result setove.

1. Profilirati event traffic, DB queryje, image cache i UI virtualizaciju.

1. Auditirati postojeći Soulseek.NET/Sockseek feature set prema pravilima: search, wishlist, download, upload, chat, privileges i sharing.

1. Donijeti ADR: javni beta, zatvoreni beta ili dodatni compliance sprintovi.

1. Dokumentirati legal-use poruku i user responsibility bez impliciranja legitimnosti svakog sadržaja.

1. Definirati issue template, security reporting i reproducible diagnostics.

### Acceptance kriteriji

- Nema nekontroliranog memory growtha u osmosatnom runu.

- 100k library search i virtualizirani prikaz zadovoljavaju definirani performance budget.

- Postoji pisana Soulseek compliance odluka; javni release nije dopušten bez nje.

- Svi provider rate-limit i token-expiry scenariji imaju recovery UX.

- Release notes jasno navode ograničenja Spotify quota i Bandcamp/MusicBrainz mogućnosti.

### Obavezni testovi

- Soak tests.

- Large data benchmarks.

- End-to-end failure matrix.

- Packaged beta smoke test na ciljanim OS-ovima.

> **Izlazni artefakt sprinta**  
> Go/no-go dokument za javnu beta verziju i stabilan beta build ako su svi gateovi zadovoljeni.

# 21. Codex razvojni runbook

## 21.1. Obavezni postupak prije svakog sprinta

- Pročitati ovaj dokument, docs/product-scope.md i sve prihvaćene ADR-ove.

- Provjeriti da je lokalna grana sinkronizirana s masterom i da CI baseline prolazi.

- Otvoriti branch codex/sprint-NN-short-name.

- Napisati kratki implementation plan u docs/sprints/sprint-NN.md prije većeg koda.

- Identificirati postojeće klase koje se ponovno koriste; ne duplicirati engine logiku.

- Odrediti testove prije promjene javnog ugovora.

## 21.2. Pravila koda

- Nullable reference types ostaju uključeni.

- Svi I/O public async API-ji prihvaćaju CancellationToken.

- Ne koristiti Task.Run za obični async I/O.

- Ne uvoditi globalno mutable stanje u novim projektima.

- DTO-i i eventi su immutable record tipovi gdje je praktično.

- Provider SDK tipovi ne izlaze iz provider projekta.

- DbContext ne izlazi iz Infrastructure projekta.

- Desktop ViewModel ne poziva HttpClient direktno; koristi typed service/client.

- Ne catchati Exception bez mapiranja/logiranja i očuvanja correlation ID-a.

- Ne spremati raw exception s tokenima ili credentialima.

- Svaka nova konfiguracijska opcija ima default, validaciju, dokumentaciju i test.

- Svaki novi endpoint ima OpenAPI metadata, error contract i integration test.

## 21.3. Branch i PR pravila

```text
Branch:
  codex/sprint-05-search-download-ui

Commit primjeri:
  feat(search-ui): add track candidate list
  test(gateway): cover cancel and next candidate
  docs(adr): record local daemon auth decision

PR mora sadržavati:
  - cilj i scope
  - arhitekturne odluke
  - test commands i rezultate
  - DB migracije
  - sigurnosne/licencne posljedice
  - screenshot ili GIF za UI promjene
  - poznata ograničenja
  - rollback plan kada mijenja persistence/packaging
```

## 21.4. Zabranjene prečice

- Ne prepisivati DownloadEngine “jer je lakše” prije gateway/parity testova.

- Ne povezivati Avalonia projekt direktno na Sockseek.Core.

- Ne spremati OAuth token u appsettings.json, SQLite plain text ili log.

- Ne dodavati YouTube/Spotify/Bandcamp audio URL ili download funkciju.

- Ne koristiti authenticated Bandcamp scraping ili korisničke cookies.

- Ne pretpostavljati da MusicBrainz account sadrži playliste.

- Ne mijenjati AGPL licencu ili skrivati source link.

- Ne izlagati daemon na 0.0.0.0 po defaultu.

- Ne uvoditi novi UI framework, bazu ili media engine bez ADR-a.

- Ne spajati fuzzy match ispod AutoMatchThreshold bez korisnikove potvrde.

## 21.5. Standardni lokalni commands

```bash
dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build

dotnet test Sockseek.Application.Tests -c Release
dotnet test Sockseek.E2E.Tests -c Release

dotnet run --project Sockseek.Server -- daemon
dotnet run --project Sockseek.Desktop

# prije PR-a
dotnet format --verify-no-changes   # samo kada je repo konfiguracija stabilna
# generiraj/provjeri OpenAPI prema postojećem build targetu
```

# 22. Globalni Definition of Done

| Kategorija | Sprint je gotov samo ako |
| --- | --- |
| Build | Release build svih projekata prolazi bez novih nedokumentiranih warninga. |
| Tests | Svi postojeći i novi relevantni testovi prolaze; flaky test se ne ignorira bez issuea. |
| Architecture | Dependency pravila nisu prekršena; nema provider/Core tipova u UI/Domain sloju. |
| API | OpenAPI je ažuriran; error i auth ponašanje testirano. |
| Persistence | Migracija, backup i upgrade test postoje kada se schema mijenja. |
| Security | Nema secreta u logovima/DB-u; novi inputi imaju validaciju. |
| UI | Loading, empty, error, disconnected i accessibility stateovi su implementirani. |
| Performance | Nema očitog N+1 ili nevirtualizirane velike liste; budget provjeren gdje je relevantno. |
| Docs | Sprint dokument, ADR i user-facing dokumentacija ažurirani su u istom PR-u. |
| License | Nove third-party ovisnosti evidentirane; AGPL obavijesti ostaju. |
| Review | PR opis sadrži reprodukciju, test rezultate i poznata ograničenja. |

# 23. MVP i javni release gateovi

## 23.1. MVP feature complete

- Onboarding i lokalni daemon rade bez CLI koraka.

- Manual Soulseek search/download i lokalni player rade.

- Lokalna biblioteka se indeksira.

- Spotify i YouTube importiraju playliste u podržanom test okruženju.

- Bandcamp public URL import i MusicBrainz enrichment rade.

- Imported playlist se može resolveati, bulk downloadati i reproducirati lokalno.

- Nema provider playback/download funkcija.

## 23.2. Public beta gate

- Sprintovi 14 i 15 završeni.

- Soulseek compliance ADR dopušta planirani način distribucije.

- AGPL source i legal artefakti dostupni su za točan build.

- Windows installer i barem Linux package imaju smoke test.

- Secret storage i localhost auth prošli su security testove.

- Provider quota i approval ograničenja jasno su prikazana korisniku.

- Nema poznatog gubitka podataka ili korupcije library/playlist baze.

# 24. Post-MVP backlog

- ListenBrainz account, playliste, history i scrobbling.

- Gapless playback, ReplayGain, crossfade samo između lokalnih datoteka.

- Tag editor i batch metadata correction.

- Remote-control web/mobile companion koji ne pokreće Soulseek klijent na mobilnom uređaju.

- Multi-device local network sync uz eksplicitnu autentikaciju.

- Lyrics provider s posebnim licencnim pregledom.

- Waveform i audio analysis cache.

- Soulseek sharing/upload/wishlist/chat proširenja ako compliance audit to zahtijeva.

- Plugin SDK za nove playlist source providere bez pristupa player internalsima.

- Public playlist sharing unutar aplikacije bez dijeljenja audio datoteka.

# 25. Početni prompt za Codex

Sljedeći prompt može se dati Codexu nakon što su ovaj dokument i izvedeni ADR-ovi dodani u repozitorij:

```text
Radi na repozitoriju k33zo33/sockseek.

Prvo pročitaj:
- docs/product-scope.md
- docs/provider-capability-matrix.md
- docs/adr/0001-agpl-product.md
- docs/adr/0002-local-first.md
- docs/adr/0003-provider-playlist-only.md
- tehničku specifikaciju Sockseek UI.

Implementiraj isključivo Sprint 0.

Zaključane odluke:
- vanjski servisi služe samo za uvoz/sinkronizaciju playlista i metapodataka;
- nema Spotify/YouTube/Bandcamp playbacka ili downloada;
- sav playback dolazi iz lokalne biblioteke ili Soulseek downloada;
- projekt ostaje GNU AGPL v3;
- postojeći Sockseek engine se ne prepisuje;
- legacy CLI i API moraju ostati funkcionalni.

Prije izmjene koda:
1. provjeri granu, remoteove i baseline commit;
2. pokreni restore/build/test;
3. napiši docs/sprints/sprint-00.md s planom;
4. ne force-pushaj master.

Na kraju:
- pokreni puni test suite;
- sažmi izmjene, test rezultate, rizike i sljedeći sprint;
- otvori PR s jasnim rollback planom.
```

# 26. Traceability matrica

| Zahtjev | Primarni sprintovi | Dokaz prihvata |
| --- | --- | --- |
| User-friendly desktop UI | 4, 5, 13 | E2E onboarding/search/playlist flow i UI screenshots. |
| Spotify playlist source | 9, 10 | OAuth/import/sync fixture i live allowlist smoke test. |
| YouTube playlist source | 9, 11 | mine=true import, pagination i deleted item test. |
| Bandcamp source | 12 | Public URL fixture import; no credential test. |
| MusicBrainz metadata | 12 | Rate-limit/cache i ISRC/MBID enrichment test. |
| Soulseek downloader | 2, 5, 13 | Gateway parity i E2E download. |
| Full local player | 7 | Codec matrix i queue persistence. |
| Play while downloading | 8 | Slow-stream/underrun/cancel tests. |
| Local library | 6 | Scan/watcher/10k performance tests. |
| AGPL path A | 0, 14 | LICENSE/About/source/release artifact checklist. |
| Existing fork as base | 0-2 | Baseline tag, upstream sync i adapter parity. |
| No external-service streaming | 0, 9-12, 13 | Provider contracts bez playback/download metoda i architecture tests. |

# 27. Izvori i referentni dokumenti

## 27.1. Codebase reference - baseline fork

| Oznaka | Referenca |
| --- | --- |
| C1 | Sockseek.sln - postojeća solution struktura. |
| C2 | Sockseek.Core/DownloadEngine.cs - engine orkestracija, queue, cancellation i job processing. |
| C3 | Sockseek.Core/Jobs/Job.cs, SongJob.cs, AlbumJob.cs, AggregateJob.cs - postojeći domenski job model. |
| C4 | Sockseek.Core/Services/Searcher.cs i SearchProjection/ResultSorter.cs - pretraga i rangiranje. |
| C5 | Sockseek.Core/Services/Downloader.cs - incomplete file, retry, progress i resume. |
| C6 | Sockseek.Core/Extractors/* - Spotify, YouTube, Bandcamp, MusicBrainz, CSV i ostali extractori. |
| C7 | Sockseek.Api/Client/SockseekApiClient.cs - postojeći typed daemon client. |
| C8 | Sockseek.Server/ServerHost.cs i EngineSupervisor.cs - REST/SignalR daemon i state management. |
| C9 | Sockseek.Core/Services/FileManager.cs, TrackSkipper.cs i M3uEditor.cs - output/library/index ponašanje. |
| C10 | LICENSE - GNU Affero General Public License v3. |

## 27.2. Vanjski službeni izvori

R1 - Spotify authorization i PKCE za desktop aplikacije: https://developer.spotify.com/documentation/web-api/concepts/authorization

R2 - Spotify Authorization Code with PKCE: https://developer.spotify.com/documentation/web-api/tutorials/code-pkce-flow

R3 - Spotify quota modes i development-mode ograničenja: https://developer.spotify.com/documentation/web-api/concepts/quota-modes

R4 - YouTube Data API playlists.list (mine=true): https://developers.google.com/youtube/v3/docs/playlists/list

R5 - Google OAuth 2.0 for iOS & Desktop Apps: https://developers.google.com/identity/protocols/oauth2/native-app

R6 - MusicBrainz API: https://musicbrainz.org/doc/MusicBrainz_API

R7 - MusicBrainz API rate limiting i User-Agent pravila: https://musicbrainz.org/doc/MusicBrainz_API/Rate_Limiting

R8 - Bandcamp API access: https://bandcamp.com/developer

R9 - Soulseek rules: https://www.slsknet.org/news/node/681

## 27.3. Napomena o promjenjivim pravilima

Provider API pravila, quota ograničenja i OAuth zahtjevi mogu se promijeniti. Prije implementacije svakog provider sprinta Codex mora ponovno provjeriti službenu dokumentaciju i zapisati datum provjere u docs/providers/<provider>.md. Ako se službena pravila razlikuju od ove specifikacije, ne implementira se riskantna funkcija; otvara se ADR za novu odluku.

# 28. Konačna implementacijska direktiva

> **Što Codex treba izgraditi**  
> Novu cross-platform desktop aplikaciju oko postojećeg Sockseek daemona: vanjski servisi uvoze playliste i metapodatke, aplikacija ih pretvara u lokalne playlist stavke, povezuje ih s lokalnom bibliotekom ili Soulseek kandidatima, preuzima nedostajuću glazbu i reproducira isključivo lokalne/Soulseek datoteke.

- Ne radi big-bang rewrite.

- Prvo stabilizira baseline i gradi gateway.

- Zatim dodaje persistence, UI, library i player.

- Provider integracije dolaze tek nakon sigurnog auth/secret frameworka.

- Unified playlist workflow dolazi kada svi temeljni dijelovi rade odvojeno.

- Javni release dolazi tek nakon packaging, security, AGPL i Soulseek compliance gateova.
