# Beta limitations and release notes

Sockseek is not approved for public beta distribution yet. Sprint 15 currently allows closed/internal beta testing only.

## Release status

- Public beta: blocked by ADR-0009 until Soulseek compliance is resolved.
- Closed/internal beta: allowed for packaging, performance, diagnostics, provider import and local playback validation.
- Stable release: blocked until public beta gates, code signing/rollback decisions and cross-platform packaging decisions are complete.
- Current Sprint 15 go/no-go evidence is tracked in `docs/beta-go-no-go.md`.

## Legal-use notice

Sockseek is a local-first music manager, Soulseek downloader and local audio player. Users are responsible for following applicable law and the terms of any network or service they use. Only share, download, import or play material that you are legally allowed to use.

External services are playlist or metadata sources only. Spotify, YouTube, Bandcamp and MusicBrainz are never audio sources for Sockseek playback or downloading.

## Provider limitations

| Provider | Current capability | Known limitation |
| --- | --- | --- |
| Spotify | OAuth PKCE account connection, playlist listing, playlist item import and sync through metadata APIs. | Spotify development-mode quota and allowlist restrictions may block unregistered users. No Spotify playback, audio streaming, audio URL use or downloading. |
| YouTube | OAuth PKCE account connection, `mine=true` playlist listing, playlist item import and sync through metadata APIs. | Token expiry/revocation requires reconnect. No YouTube playback, iframe player, audio extraction, offline cache or downloading. |
| Bandcamp | Public album/track URL metadata import without credentials. | Parser depends on public page structured metadata; failures are localized to import. No cookies, credentials, playback or downloading from Bandcamp. |
| MusicBrainz | Metadata lookup/enrichment with rate limiting and cache behavior. | Metadata-only; no MusicBrainz account, ListenBrainz history, playback, download or scrobbling support. |
| Soulseek | Search, candidate ranking, album folder discovery, download workflows and local/progressive playback of downloaded files. | Public beta is blocked until compliance coverage for wishlist, upload/sharing, chat and privilege recognition is resolved. |

## Operational limitations

- Windows release-candidate packaging is internally verified with legal artifacts, SBOM and SHA256 manifest.
- The Windows installer is currently script-based PowerShell, not a signed MSI/MSIX.
- Docker is a secondary headless path and was not fully smoke-tested locally because Docker Desktop's engine did not respond from the validation session. The Docker smoke helper now fails hung Docker CLI commands with a bounded timeout instead of blocking indefinitely.
- Linux desktop packaging is not complete; the current native media package path is Windows-focused.
- The Sprint 15 eight-hour soak gate passed for commit `93a8c63`; rendered UI virtualization traces are still open and require manual capture in a real Desktop window.

## Diagnostics for closed beta testers

- Use the app `Copy diagnostics` action when it is available, or the smallest redacted crash detail or log excerpt needed to reproduce an issue.
- Follow `docs/diagnostics-feedback.md` for crash reports, reproducible diagnostics and routing.
- Do not paste provider tokens, OAuth codes, PKCE code verifiers, client secrets, Soulseek passwords, full Authorization headers or private playlist URLs with sensitive query parameters.
- Report vulnerabilities privately through the process in `SECURITY.md`.
