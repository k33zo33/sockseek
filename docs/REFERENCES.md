# References and source material

## 27. Izvori i referentni dokumenti

### 27.1. Codebase reference - baseline fork

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

### 27.2. Vanjski službeni izvori

R1 - Spotify authorization i PKCE za desktop aplikacije: https://developer.spotify.com/documentation/web-api/concepts/authorization

R2 - Spotify Authorization Code with PKCE: https://developer.spotify.com/documentation/web-api/tutorials/code-pkce-flow

R3 - Spotify quota modes i development-mode ograničenja: https://developer.spotify.com/documentation/web-api/concepts/quota-modes

R4 - YouTube Data API playlists.list (mine=true): https://developers.google.com/youtube/v3/docs/playlists/list

R5 - Google OAuth 2.0 for iOS & Desktop Apps: https://developers.google.com/identity/protocols/oauth2/native-app

R6 - MusicBrainz API: https://musicbrainz.org/doc/MusicBrainz_API

R7 - MusicBrainz API rate limiting i User-Agent pravila: https://musicbrainz.org/doc/MusicBrainz_API/Rate_Limiting

R8 - Bandcamp API access: https://bandcamp.com/developer

R9 - Soulseek rules: https://www.slsknet.org/news/node/681

### 27.3. Napomena o promjenjivim pravilima

Provider API pravila, quota ograničenja i OAuth zahtjevi mogu se promijeniti. Prije implementacije svakog provider sprinta Codex mora ponovno provjeriti službenu dokumentaciju i zapisati datum provjere u docs/providers/<provider>.md. Ako se službena pravila razlikuju od ove specifikacije, ne implementira se riskantna funkcija; otvara se ADR za novu odluku.
