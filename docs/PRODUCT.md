# Product scope and release boundaries

This document defines what the product is, what it is not, and which decisions are locked.

## 1. Izvršni sažetak

Cilj je pretvoriti postojeći Sockseek fork u pristupačnu desktop glazbenu aplikaciju koja izgleda i ponaša se kao moderan player, ali koristi postojeći Soulseek engine za pronalaženje i preuzimanje glazbe. Korisnik može povezati podržane račune ili unijeti javnu playlistu, uvesti popis pjesama, riješiti stavke prema lokalnoj biblioteci ili Soulseek rezultatima, preuzeti nedostajuće pjesme i reproducirati ih iz iste aplikacije.

Spotify i YouTube bit će izvori korisničkih playlista. Bandcamp će u prvoj javnoj verziji podržavati javne URL-ove jer službeni API nije opći fan-account API. MusicBrainz se koristi kao javni metadata provider, a ne kao izvor playlista; za MetaBrainz korisničke playliste može se naknadno dodati ListenBrainz. Nijedan od tih servisa ne smije biti audio source unutar našeg playera.

Tehnički pristup je evolucijski: postojeći projekti Sockseek.Core, Sockseek.Api, Sockseek.Server i Sockseek.Cli ostaju funkcionalni. Dodaju se novi domenski, aplikacijski, infrastrukturni, integracijski, player i desktop slojevi. UI se spaja na lokalni daemon preko postojećeg HTTP/SignalR temelja. Time se smanjuje rizik i čuva dokazana logika pretrage, rangiranja, downloada, retryja, cancellationa i workflowa.

> **Konačna definicija proizvoda**  
> Sockseek UI je local-first desktop glazbeni player i downloader. Vanjske playliste predstavljaju željeni popis pjesama; aplikacija ih povezuje s lokalnim datotekama ili Soulseek rezultatima. Reprodukcija se obavlja isključivo iz lokalne datoteke, dovršenog downloada ili djelomično preuzete Soulseek datoteke kroz kontrolirani “Play while downloading” način.

### 1.1. Ključni ishodi MVP-a

- Moderan cross-platform desktop UI s onboardingom, pretragom, playlistama, bibliotekom, download queueom i playerom.

- Povezivanje Spotify i YouTube računa samo radi čitanja playlista i njihovih stavki.

- Import javnih Bandcamp URL-ova bez autentificiranog scrapinga.

- MusicBrainz metadata enrichment i identifikacija pjesama preko MBID/ISRC vrijednosti.

- Ujedinjena lokalna playlist struktura koja čuva izvorni provider i status svake stavke.

- Soulseek search, candidate review, bulk download i retry preko postojećeg enginea.

- Lokalni player s queueom, shuffle/repeat kontrolama i podržanim audio formatima.

- Opcionalni “Play while downloading” za formate koji prolaze capability test.

- AGPL-compliant distribucija s vidljivom licencom i dostupnim izvornim kodom.

### 1.2. MVP nije

- Spotify, YouTube, Bandcamp ili drugi streaming player.

- Alat za preuzimanje audija s YouTubea ili Spotifyja.

- Centralizirani cloud Soulseek servis za velik broj korisnika.

- Mobilna aplikacija ili web SaaS u prvoj fazi.

- Potpuno nova implementacija Soulseek protokola.

- Automatski downloader svih stavki bez korisnikove jasne akcije ili konfigurirane politike.

## 2. Zaključane odluke proizvoda

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

### 2.1. Funkcionalni opseg po provideru

| Provider | MVP mogućnost | Nije dopušteno / nije dostupno | Status |
| --- | --- | --- | --- |
| Spotify | OAuth PKCE, popis korisničkih playlista, čitanje stavki, opcionalno saved tracks kao sintetička playlista. | Nema Spotify playbacka, audio streama ni “download from Spotify”. Development mode ima ograničen broj korisnika. | Implementirati iza feature flaga i jasnog quota upozorenja. |
| YouTube | Google OAuth, popis playlista korisnika, čitanje video metapodataka i redoslijeda. | Nema iframe playera, audio extractiona, YouTube downloada ni offline cachea. | Implementirati kao playlist import. |
| Bandcamp | Import javnog album/track URL-a i metapodataka; kasnije import javno dostupne kolekcije ako postoji stabilan službeni put. | Nema općeg fan OAuth API-ja; nema authenticated scrapinga ni spremanja cookiesa. | MVP: public URL import. |
| MusicBrainz | Javni metadata lookup, MBID/ISRC enrichment, kanonski artist/release/recording podaci. | MusicBrainz nema korisničke playliste za import. | Metadata provider bez obveznog accounta. |
| ListenBrainz | Opcionalno u kasnijem sprintu: korisničke playliste i povijest kao MetaBrainz account integracija. | Nije zamjena za MusicBrainz metadata model. | Post-MVP ili dio MetaBrainz sprinta. |
| Soulseek | Search, album folder discovery, download, retry, candidate ranking, progressive local playback. | Nije centralizirani cloud servis; mora se napraviti compliance audit prije javnog releasea. | Glavni audio backend. |

### 2.2. Važna ograničenja dostupnosti

- Spotify desktop aplikacija mora koristiti Authorization Code with PKCE jer ne može sigurno čuvati client secret. Spotify development mode trenutno je ograničen na najviše pet allowlistanih korisnika; šira distribucija ovisi o Spotify odobrenju i quota modu [R1-R3].

- YouTube Data API može vratiti playliste vlasnika autentificiranog računa kroz autorizirani zahtjev s mine=true; desktop OAuth koristi system browser i lokalni redirect URI [R4-R5].

- Bandcamp službeni API namijenjen je labelima i merchandise fulfillment partnerima, pa obični fan account connect nije dio MVP-a [R8].

- MusicBrainz treba globalni limiter od najviše približno jednog zahtjeva u sekundi po IP-u i smislen User-Agent [R6-R7].

- Soulseek pravila navode da automatizirani klijenti bez punog skupa funkcija nisu dopušteni te toleriraju alternativne klijente s punim funkcijama; prije javnog releasea potreban je poseban compliance sprint [R9].

## 23. MVP i javni release gateovi

### 23.1. MVP feature complete

- Onboarding i lokalni daemon rade bez CLI koraka.

- Manual Soulseek search/download i lokalni player rade.

- Lokalna biblioteka se indeksira.

- Spotify i YouTube importiraju playliste u podržanom test okruženju.

- Bandcamp public URL import i MusicBrainz enrichment rade.

- Imported playlist se može resolveati, bulk downloadati i reproducirati lokalno.

- Nema provider playback/download funkcija.

### 23.2. Public beta gate

- Sprintovi 14 i 15 završeni.

- Soulseek compliance ADR dopušta planirani način distribucije.

- AGPL source i legal artefakti dostupni su za točan build.

- Windows installer i barem Linux package imaju smoke test.

- Secret storage i localhost auth prošli su security testove.

- Provider quota i approval ograničenja jasno su prikazana korisniku.

- Nema poznatog gubitka podataka ili korupcije library/playlist baze.

## 24. Post-MVP backlog

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

## 28. Konačna implementacijska direktiva

> **Što Codex treba izgraditi**  
> Novu cross-platform desktop aplikaciju oko postojećeg Sockseek daemona: vanjski servisi uvoze playliste i metapodatke, aplikacija ih pretvara u lokalne playlist stavke, povezuje ih s lokalnom bibliotekom ili Soulseek kandidatima, preuzima nedostajuću glazbu i reproducira isključivo lokalne/Soulseek datoteke.

- Ne radi big-bang rewrite.

- Prvo stabilizira baseline i gradi gateway.

- Zatim dodaje persistence, UI, library i player.

- Provider integracije dolaze tek nakon sigurnog auth/secret frameworka.

- Unified playlist workflow dolazi kada svi temeljni dijelovi rade odvojeno.

- Javni release dolazi tek nakon packaging, security, AGPL i Soulseek compliance gateova.
