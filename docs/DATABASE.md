# Persistence and database

## 8. Persistence i baza podataka

Koristi se SQLite s EF Core migracijama. Baza je lokalna i pripada jednom korisničkom profilu aplikacije. Provider tokeni ne spremaju se u SQLite u čitljivom obliku.

### 8.1. Predložene tablice

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

### 8.2. Migracijska pravila

- Svaka schema promjena mora imati EF Core migraciju i test nadogradnje s prethodne release baze.

- Prije destructive migracije automatski napraviti kopiju baze u backup direktorij.

- Migracije se izvršavaju u daemonu prije prihvaćanja UI konekcije.

- Downgrade nije podržan; rollback releasea mora vratiti backup baze.

- Raw provider metadata može se čuvati kao JSON radi kompatibilnosti, ali ključna polja moraju biti normalizirana u stupce.

- Datoteke i glazba nikad se ne brišu samo zato što je provider stavka uklonjena iz source playliste.
