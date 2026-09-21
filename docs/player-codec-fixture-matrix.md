# Player codec fixture matrix

Sprint 7 validates local-file startup through `LibVlcMediaEngineFixtureTests`.
Fixtures are 0.5 second sine-wave files generated locally with ffmpeg and stored under
`Sockseek.Player.Tests/Fixtures/PlayerCodec`.

| Codec | Fixture | Container | Sprint 7 local playback result |
| --- | --- | --- | --- |
| MP3 | `tone.mp3` | MP3 | Pass: LibVLC starts local playback without error. |
| FLAC | `tone.flac` | FLAC | Pass: LibVLC starts local playback without error. |
| Ogg Vorbis | `tone.ogg` | Ogg | Pass: LibVLC starts local playback without error. |
| Opus | `tone.opus` | Ogg Opus | Pass: LibVLC starts local playback without error. |
| WAV | `tone.wav` | WAV PCM | Pass: LibVLC starts local playback without error. |
| AAC/M4A | `tone.m4a` | MPEG-4 audio | Pass: LibVLC starts local playback without error. |

Scope notes:

- This matrix covers completed local files only.
- Progressive playback while downloading remains Sprint 8 scope.
- Tests use dummy audio output and do not introduce provider audio URLs or external media sources.
