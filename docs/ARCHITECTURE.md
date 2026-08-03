# Target architecture

## 5. Ciljna arhitektura

### 5.1. Arhitekturna načela

- Local-first: glazba, credentials, provider tokeni i baza ostaju lokalni.

- API-first: Desktop UI komunicira s daemonom kroz verzionirani HTTP API i SignalR evente.

- Strangler pattern: novi slojevi postupno obavijaju postojeći Sockseek engine.

- Capability-driven providers: svaki provider deklarira što službeno podržava.

- Immutable UI state: UI dobiva DTO snapshotove, nikad mutable Core objekte.

- Idempotent sync: ponovljeni import iste playliste ne duplicira stavke.

- Explicit user action: download i spajanje nejasnih rezultata moraju biti vidljivi i poništivi.

- Secure by default: localhost only, PKCE, secret store, log redaction.

- Backward compatibility: CLI i postojeći daemon API ostaju funkcionalni do eksplicitne deprecacije.

### 5.2. Procesna topologija

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

### 5.3. Zašto odvojeni daemon proces

- UI crash ne prekida nužno aktivne downloade.

- Postojeći remote CLI i budući remote-control klijent mogu koristiti isti API.

- OAuth callback, library scan i player ostaju u kontroliranom backend procesu.

- Moguće je pokretati daemon bez UI-ja za napredne korisnike.

- Engine logovi i crash recovery mogu se odvojiti od UI render loopa.

### 5.4. Ciljana struktura solutiona

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

### 5.5. Pravila ovisnosti

| Projekt | Smije ovisiti o | Ne smije ovisiti o |
| --- | --- | --- |
| Domain | Samo BCL. | EF Core, Avalonia, Spotify SDK, Soulseek.Core, filesystem. |
| Application | Domain, Integrations.Abstractions. | Avalonia, konkretni provider SDK-i, konkretna baza. |
| Infrastructure | Application, Domain. | Desktop UI. |
| Integrations.* | Application, Domain, Abstractions. | Desktop UI, Soulseek.Core internals. |
| Player | Application, Domain. | Provider SDK-i; vanjski streaming servisi. |
| Server | Application, Infrastructure, Player, adapter prema Coreu. | Avalonia. |
| Desktop | Sockseek.Api contracts/client, UI toolkit. | Sockseek.Core, EF DbContext, provider SDK-i. |

## 6. Komponente i odgovornosti

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

## 11. Integracija s postojećim Soulseek engineom

### 11.1. Gateway contract

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

### 11.2. Adapter pravila

- Adapter je jedino novo mjesto koje smije poznavati Sockseek.Core Job tipove.

- Mapiranje je jednosmjerno: Core Job -> immutable DTO snapshot. UI nikad ne mutira Core Job.

- Postojeći EngineSupervisor i SockseekApiClient koriste se gdje je moguće umjesto direktnog pozivanja privatnih metoda.

- Novi application workflow ID mora se mapirati na postojeći Core WorkflowId.

- Engine DisplayId je prikazni podatak, ne trajni ključ u bazi.

- Core evente koalescirati prije slanja UI-ju kako progress eventovi ne bi preplavili render thread.

- Cancellation i next-candidate moraju ostati kompatibilni s postojećim CLI ponašanjem.

### 11.3. Što se ne refaktorira u prvoj fazi

- Unutarnji Searcher i Downloader algoritmi.

- ResultSorter kriteriji, osim dodatnih testova i bugfixeva.

- Existing extractor registry, osim uklanjanja provider playback/download koncepata iz novog UI-ja.

- Job state model, sve dok DTO adapter daje konzistentne snapshotove.

- Legacy CLI configuration binding.
