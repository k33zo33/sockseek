## Sprint 9 - Provider framework i secret store

## Status

Complete

## Required context

Always read `/AGENTS.md` and `/docs/project-state.yaml`, then:

- [PROVIDERS.md](../PROVIDERS.md)
- [SECURITY.md](../SECURITY.md)
- [DATABASE.md](../DATABASE.md)

## Scope rule

Do not implement future sprint scope. Stop and request an ADR if a locked decision must change.

> **Cilj sprinta**  
> Postaviti siguran i capability-driven temelj prije dodavanja stvarnih account providera.

Ovisnosti: Sprintovi 1, 3 i 4.

### Isporučivi rezultati

- IPlaylistSourceProvider i capability registry.

- OAuth coordinator s PKCE/state/loopback callbackom.

- ISecretStore platform abstraction.

- Accounts UI i provider status modeli.

### Implementacijski zadaci

1. Implementirati fake provider za E2E testove.

1. Implementirati Windows secret store; Linux/macOS adaptere planirati ili implementirati prema targetu.

1. Dodati provider HTTP pipeline s retry/429/backoff pravilima.

1. Dodati log redaction handler.

1. Implementirati connect/disconnect lifecycle i expired state.

1. UI mora capabilityjima sakriti nepodržane akcije.

### Acceptance kriteriji

- Access i refresh token ne postoje u SQLiteu ni logovima.

- PKCE state mismatch se odbija.

- Fake provider može importirati i sinkronizirati playlistu.

- Disconnect briše secret i account status se ažurira.

- Bandcamp capability ne prikazuje Connect account.

### Obavezni testovi

- OAuth callback adversarial tests.

- Secret store integration tests.

- Redaction tests.

- Provider capability UI tests.

> **Izlazni artefakt sprinta**  
> Siguran provider framework spreman za Spotify i YouTube bez dupliciranja auth logike.

## Completion report

Completed in local commits `43587af` through `6a45965`.

Changed files and areas:

- Provider abstractions and capability registry:
  - `Sockseek.Integrations.Abstractions/ProviderCapabilities.cs`
  - `Sockseek.Application/Providers/ProviderCapabilityRegistry.cs`
  - `Sockseek.Application.Tests/Providers/ProviderCapabilityRegistryTests.cs`
- Secret store and redaction:
  - `Sockseek.Application/Security/ISecretStore.cs`
  - `Sockseek.Application/Security/SensitiveLogRedactor.cs`
  - `Sockseek.Infrastructure/Security/InMemorySecretStore.cs`
  - `Sockseek.Infrastructure/Security/WindowsDpapiSecretStore.cs`
  - `Sockseek.Infrastructure/Security/SecretStoreValidation.cs`
  - `Sockseek.Application.Tests/Security/SensitiveLogRedactorTests.cs`
  - `Sockseek.Infrastructure.Tests/Security/SecretStoreTests.cs`
- OAuth PKCE/state/loopback and provider HTTP resilience:
  - `Sockseek.Application/Providers/OAuthPkceCoordinator.cs`
  - `Sockseek.Application/Providers/OAuthLoopbackCallbackListener.cs`
  - `Sockseek.Application/Providers/ProviderHttpRetryHandler.cs`
  - `Sockseek.Application.Tests/Providers/OAuthPkceCoordinatorTests.cs`
  - `Sockseek.Application.Tests/Providers/OAuthLoopbackCallbackListenerTests.cs`
  - `Sockseek.Application.Tests/Providers/ProviderHttpRetryHandlerTests.cs`
- Fake provider and account lifecycle:
  - `Sockseek.Integrations.Fake/*`
  - `Sockseek.Infrastructure/Persistence/ExternalAccountStore.cs`
  - `Sockseek.Infrastructure.Tests/Persistence/ExternalAccountStoreTests.cs`
  - `Sockseek.Application.Tests/Providers/FakePlaylistSourceProviderTests.cs`
- Server/API/Desktop account and provider surfaces:
  - `Sockseek.Api/Client/SockseekApiClient.cs`
  - `Sockseek.Api/Client/SockseekApiJsonContext.cs`
  - `Sockseek.Api/Contracts/ServerResponses.cs`
  - `Sockseek.Server/ServerHost.cs`
  - `Sockseek.Server.Tests/ProviderEndpointTests.cs`
  - `Sockseek.Desktop/ProviderConnectionCardViewModel.cs`
  - `Sockseek.Desktop/DesktopAccountsViewModel.cs`
  - `Sockseek.Desktop/DesktopShellMainWindow.axaml`
  - `Sockseek.Desktop/DesktopShellWindowViewModel.cs`
  - `Sockseek.Desktop.Tests/ProviderConnectionCardViewModelTests.cs`
  - `Sockseek.Desktop.Tests/DesktopAccountsViewModelTests.cs`
  - `docs/openapi.json`

Validation commands and results:

- `dotnet restore`: passed.
- `dotnet build -c Release`: passed.
- `dotnet test -c Release --no-build`: passed.
- Targeted provider/security tests passed for Application, Infrastructure, Server and Desktop.
- `git diff --check`: passed.
- Provider-audio forbidden symbol scan for `IPlaybackProvider`, `GetAudioStreamAsync`, `DownloadTrackAsync`, provider audio URLs and stream URLs: no matches.

Acceptance criteria:

- Access and refresh token values are stored through `ISecretStore`; SQLite account rows store only opaque `SecretReference` values. Covered by secret store tests, fake provider tests and API response tests that reject secret/token leakage.
- PKCE state mismatch, provider/redirect mismatch, expired state, missing code and reused state are rejected by OAuth adversarial tests.
- Fake provider can authorize, import and sync playlist snapshots.
- Disconnect deletes the secret reference through `ISecretStore`, clears `SecretReference`, marks the account disconnected and preserves local playlist/media data.
- Expired authorization state can be marked without deleting local data or the opaque secret reference.
- Bandcamp capabilities do not expose Connect account in registry, API DTOs or Desktop account cards.

Migrations:

- No new EF migration was required; existing `ExternalAccounts.SecretReference` and status fields cover Sprint 9.

Security, privacy and license impact:

- No provider playback, audio stream, audio URL or external download capability was introduced.
- API and Desktop surfaces expose only provider/account status and capability metadata, never token material.
- AGPL-3.0 posture is unchanged.

Known risks and unmet criteria:

- Real Spotify/YouTube provider adapters are intentionally out of scope until later sprints.
- Windows DPAPI integration is implemented for the first target; Linux/macOS secret store adapters remain future target work.
- Existing NuGet advisory warnings remain documented build output: AngleSharp moderate and SQLitePCLRaw high.
