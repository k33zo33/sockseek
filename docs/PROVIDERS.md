# External providers and playlist synchronization

External providers are playlist and metadata sources only. They never provide audio to the internal player.

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

## 9. Integracijski ugovori

### 9.1. Provider capabilities

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

### 9.2. Minimalni provider interface

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

### 9.3. Normalizirani vanjski track DTO

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

### 9.4. OAuth i tajne

- Desktop OAuth koristi system browser, PKCE, state i loopback redirect.

- UI dobiva samo status povezivanja i javni account profil; access/refresh token nikad ne ide u UI proces.

- Tokeni se spremaju preko ISecretStore; SQLite sadrži samo SecretReference.

- Refresh se obavlja unutar provider adaptera uz per-account lock da se izbjegne paralelni refresh race.

- Disconnect prvo revokea token ako provider podržava revoke, zatim briše secret i označava account disconnected.

- Log filter mora prepoznati Authorization, access_token, refresh_token, code_verifier i client_secret.

## 10. Uvoz, sinkronizacija i rješavanje playlista

### 10.1. Načini importa

| Način | Ponašanje | Kada koristiti |
| --- | --- | --- |
| Copy | Jednokratni snapshot. Nakon importa lokalna playlista je neovisna o provideru. | Default za javne URL-ove i korisnike koji žele ručno uređivati. |
| Mirror | Source playlist ostaje autoritet za redoslijed i članstvo; lokalni resolution/download podaci se čuvaju. | Spotify/YouTube povezani račun uz uključenu sinkronizaciju. |
| Append | Novi provider itemi se dodaju, ali uklanjanja na provideru ne uklanjaju lokalne stavke. | Arhivske i “collect forever” playliste. |

### 10.2. Idempotentni sync algoritam

- Dohvati provider snapshot sa stabilnim playlist item ili track ID vrijednostima.

- Normaliziraj sve stavke u ExternalTrackSnapshot.

- Usporedi po ProviderItemId; ne uspoređuj samo po poziciji.

- Ažuriraj naziv, poziciju i metadata snapshot bez brisanja CanonicalTrack veze.

- Nove stavke dodaj kao Imported/Unresolved.

- U Mirror modu nestale stavke označi RemovedAtUtc; ne briši lokalnu datoteku niti download record.

- Promjenu pozicije odradi u jednoj transakciji s privremenim sort keyem da se izbjegnu unique konflikti.

- Spremi provider cursor/ETag ako je dostupan.

- Emitiraj PlaylistSyncCompleted event s diff brojevima.

### 10.3. Resolution pipeline

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

### 10.4. Bulk playlist operacije

- Resolve all unresolved items.

- Download all missing items.

- Download selected items.

- Retry failed items.

- Apply quality profile to selected items.

- Skip item without deleting it.

- Manually map to local file.

- Manually choose Soulseek candidate.

- Remove from local playlist without modifying provider unless posebna write-back funkcija bude kasnije odobrena.
