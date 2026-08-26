# Testing, CI/CD and packaging

## 17. Testna strategija

| Razina | Obuhvat | Alati / pristup |
| --- | --- | --- |
| Unit | Domain score, state transitions, sync diff, queue logic, path safety. | MSTest postojeći standard ili jedan konsolidirani framework; bez mreže. |
| Integration | EF migracije, provider HTTP adapteri, daemon auth, player adapter. | Temp SQLite, fake HTTP server, fixture odgovori. |
| Contract | OpenAPI i provider DTO mapping. | Snapshot tests; backward compatibility za legacy API. |
| Core parity | Local/remote gateway rezultat isti kao postojeći CLI backend. | Existing mock Soulseek client i parity testovi. |
| UI component | ViewModel commands, loading/error state, list virtualizacija. | Avalonia headless tests. |
| E2E | Onboarding, import, resolve, download, playback, restart. | Packaged daemon + desktop test harness i mock providers. |
| Performance | 10k/100k library entries, velika playlista, veliki Soulseek result set. | BenchmarkDotNet i deterministic fixtures. |
| Security | Token redaction, traversal, auth, secret deletion. | Automated adversarial integration tests. |

### 17.1. Minimalna test matrica

- Windows 11 x64: obavezni MVP target.

- Ubuntu LTS x64: obavezni prije beta releasea.

- macOS arm64: build i smoke test prije označavanja cross-platform stable.

- SQLite upgrade iz svake javne release verzije.

- MP3, FLAC, Ogg, Opus, WAV, AAC/M4A player fixtures.

- Provider response fixtures za pagination, token expiry, 401, 403, 429 i malformed item.

- Soulseek disconnect tijekom searcha i downloada.

- Aplikacija restartana tijekom active download workflowa.

## 18. CI/CD i packaging

### 18.1. CI pipeline

```text
restore
  -> build Release
  -> unit tests
  -> integration tests
  -> architecture tests
  -> OpenAPI drift check
  -> UI build/headless tests
  -> dependency/license scan
  -> package smoke build
  -> artifacts
```

- Central Package Management za nove projekte; package versions se ne rasipaju po csproj datotekama.

- NuGet lock files ostaju uključeni.

- Generated files moraju se provjeravati za drift u CI-u.

- Release build je self-contained za ciljani RID.

- VLC/native media dependency pakira se po OS-u i evidentira u third-party notices.

- Docker nije glavni desktop distribution mehanizam; Dockerfile se ipak ažurira za headless daemon.

### 18.2. Desktop package

- Sockseek.Desktop executable i self-contained daemon executable.

- Installer kreira user-level data/config/log directories.

- Single-instance lock i deep-link/loopback OAuth callback handling.

- Auto-update nije uključen dok signature i rollback nisu definirani.

- Database backup prije updatea koji sadrži migraciju.

- About, license, source link i version/commit metadata.
