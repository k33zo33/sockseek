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

## Progressive playback smoke

Sprint 8 adds a Windows-target growing-file regression for MP3. The test writes the
first part of `tone-long.mp3` to a local `.mp3.incomplete` path, starts LibVLC before
the final bytes arrive, appends the remaining bytes and then stops playback. This is
the evidence used by the experimental progressive playback capability flag to mark
MP3 as progressive-capable. Other codecs remain local-file-only until they get the
same growing-file coverage.

Scope notes:

- Completed local-file playback is validated for every codec listed above.
- Progressive playback is validated only for the MP3 growing-file smoke test.
- Tests use dummy audio output and do not introduce provider audio URLs or external media sources.
