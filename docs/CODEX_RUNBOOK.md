# Codex development runbook

## 21. Codex razvojni runbook

### 21.1. Obavezni postupak prije svakog sprinta

- Pročitati ovaj dokument, docs/product-scope.md i sve prihvaćene ADR-ove.

- Provjeriti da je lokalna grana sinkronizirana s masterom i da CI baseline prolazi.

- Otvoriti branch codex/sprint-NN-short-name.

- Napisati kratki implementation plan u docs/sprints/sprint-NN.md prije većeg koda.

- Identificirati postojeće klase koje se ponovno koriste; ne duplicirati engine logiku.

- Odrediti testove prije promjene javnog ugovora.

### 21.2. Pravila koda

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

### 21.3. Branch i PR pravila

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

### 21.4. Zabranjene prečice

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

### 21.5. Standardni lokalni commands

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

## 22. Globalni Definition of Done

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

## 25. Početni prompt za Codex

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

## 28. Konačna implementacijska direktiva

> **Što Codex treba izgraditi**  
> Novu cross-platform desktop aplikaciju oko postojećeg Sockseek daemona: vanjski servisi uvoze playliste i metapodatke, aplikacija ih pretvara u lokalne playlist stavke, povezuje ih s lokalnom bibliotekom ili Soulseek kandidatima, preuzima nedostajuću glazbu i reproducira isključivo lokalne/Soulseek datoteke.

- Ne radi big-bang rewrite.

- Prvo stabilizira baseline i gradi gateway.

- Zatim dodaje persistence, UI, library i player.

- Provider integracije dolaze tek nakon sigurnog auth/secret frameworka.

- Unified playlist workflow dolazi kada svi temeljni dijelovi rade odvojeno.

- Javni release dolazi tek nakon packaging, security, AGPL i Soulseek compliance gateova.
