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

## Long playback smoke

`tone-long.mp3` is a 3 second local MP3 fixture. `LibVlcMediaEngineFixtureTests`
loads it through LibVLC with dummy audio output, keeps playback running for 2 seconds,
and stops it explicitly. This covers the Sprint 7 long playback smoke requirement
without relying on external audio devices or provider media sources.

Scope notes:

- This matrix covers completed local files only.
- Progressive playback while downloading remains Sprint 8 scope.
- Tests use dummy audio output and do not introduce provider audio URLs or external media sources.
