# Local player and progressive Soulseek playback

## 12. Player arhitektura

Player je novi application subsystem. Ne koristi Spotify, YouTube, Bandcamp ni drugi provider kao audio source. MediaSourceResolver vraća samo lokalnu datoteku ili datoteku koju trenutno piše Soulseek downloader.

### 12.1. Player komponente

| Komponenta | Odgovornost |
| --- | --- |
| PlaybackCoordinator | Jedini vlasnik player state machinea, queuea i komandi. |
| IMediaEngine | Apstrakcija nad LibVLCSharp ili drugim odabranim lokalnim media engineom. |
| MediaSourceResolver | Odabire najbolju lokalnu datoteku ili pokreće resolve/download workflow. |
| PlaybackQueueStore | Trajno čuva queue, current index, shuffle seed i repeat mode. |
| ProgressivePlaybackCoordinator | Provjerava capability, buffer i rast datoteke. |
| MediaMetadataReader | Čita trajanje, codec, tags, ReplayGain i cover art iz lokalne datoteke. |
| SystemMediaSessionBridge | Media keys, OS now-playing metadata i headset kontrole. |

### 12.2. Player state machine

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

### 12.3. MVP player funkcije

- Play, pause, stop, next, previous.

- Seek unutar dostupnog raspona.

- Volume i mute.

- Queue add/remove/reorder/clear.

- Repeat none/one/all i deterministic shuffle.

- Media keys i osnovni OS now-playing metadata.

- Resume posljednjeg queuea nakon restarta.

- Podrška za MP3, FLAC, Ogg/Vorbis, Opus, WAV, AAC/M4A prema capability testu media enginea.

- Error state po stavci bez rušenja cijelog playera.

### 12.4. Play while downloading

Ova funkcija nije mrežni streaming servis. Downloader i dalje stvara lokalnu .incomplete datoteku; player je otvara tek kada ima dovoljno podataka. Funkcija mora biti eksperimentalna dok svaki codec ne prođe testove.

- Capability matrica određuje podržava li format čitanje rastuće datoteke.

- Minimalni početni buffer računa se kao max(configured seconds × procijenjeni bitrate, minimal bytes).

- Player smije seekati samo unutar buffered rangea.

- Ako download speed padne ispod playback ratea, stanje prelazi u Buffering.

- Promjena kandidata zatvara media source, čisti stari buffer i ponovno ulazi u ResolvingSource.

- Ako format nije progressive-safe, playback počinje tek nakon potpunog downloada.

- Nedovršena datoteka se ne indeksira kao trajna library datoteka.
