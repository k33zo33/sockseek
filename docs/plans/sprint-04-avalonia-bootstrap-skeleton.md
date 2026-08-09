# Sprint 4 Avalonia bootstrap skeleton

## Goal

Plan the smallest safe path from the current headless desktop shell runtime to a real Avalonia application bootstrap that still preserves Sprint 4’s existing daemon/session foundations.

## Current-state findings

- `Sockseek.Desktop/Sockseek.Desktop.csproj` is still a plain `Microsoft.NET.Sdk` executable with no Avalonia package references.
- `Program.cs` currently composes `DesktopProgramBootstrap`, `DesktopShellSession`, and `DesktopShellWindowHost`, but the host only waits on cancellation through an injected async delegate.
- `DesktopShellWindowViewModel` already aggregates top-level shell state needed by a future desktop window: title, navigation, page metadata, backend banner, diagnostics, command palette state, theme, and player placeholder metadata.
- `IDesktopShellHost` and `IDesktopShellSession` already provide a seam where a real Avalonia lifetime can replace the current headless wait loop without changing daemon/session ownership.
- ADR-0004 locks the desktop framework decision to Avalonia + MVVM, so the remaining gap is no longer architectural choice; it is an implementation/bootstrap gap.

## In scope

- Define the first concrete Avalonia bootstrap slice that can sit on top of the existing shell/session abstractions.
- Identify the minimal project/package/runtime changes required to replace the headless host with a real Avalonia application lifetime.
- Define how the existing `DesktopShellWindowViewModel` should be surfaced to an Avalonia `Window` without breaking current testable composition seams.
- Identify the smallest validation/test additions needed for the first Avalonia bootstrap slice.

## Out of scope

- Implementing full page layouts, styling, or rich XAML composition.
- Reworking daemon supervision, handshake parsing, or SignalR recovery behavior.
- Introducing future-sprint playback, downloads, onboarding, or provider UI.
- Packaging/publishing work beyond what is needed to bootstrap a development shell.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-avalonia-bootstrap-skeleton.md`

## API, schema and event changes

- No server API changes.
- No schema or migration changes.
- Desktop runtime composition would gain Avalonia application/bootstrap wiring while preserving existing shell/session contracts.

## Implementation sequence

1. Convert `Sockseek.Desktop` to the minimal Avalonia-ready project shape and add the smallest required Avalonia package set.
2. Introduce an Avalonia `Application` entrypoint and a thin desktop shell host that owns the main window lifetime.
3. Add a minimal main window that binds to `DesktopShellWindowViewModel` without reworking existing shell state models.
4. Keep current `DesktopProgramBootstrap` / `DesktopShellSession` ownership intact so startup, daemon launch, and recovery logic remain testable outside Avalonia-specific code.
5. Add focused tests for the new host/bootstrap seam and keep existing headless ViewModel/navigation tests green.

## Testing strategy

- Preserve existing unit tests around `DesktopProgramBootstrap`, `DesktopShellSession`, `DesktopShellWindowViewModel`, and navigation.
- Add focused tests around any new Avalonia-specific host adapter only if they can run headlessly and deterministically.
- Run `dotnet restore`, `dotnet build -c Release`, and targeted desktop tests once the runtime has a working .NET toolchain.

## Migration and rollback

- No migration.
- Rollback is a normal revert of desktop bootstrap/project-file changes.

## Security, privacy and license impact

- Reuses the existing localhost-only daemon handshake and bearer-token flow.
- Does not widen secret storage or logging scope.
- No license impact; remains aligned with the accepted Avalonia decision.

## Risks and stop conditions

- Stop if Avalonia bootstrap requires changing the locked process topology or moving daemon ownership out of the current shell/session abstractions.
- Stop if the minimal package set forces a platform/runtime decision that belongs in Sprint 14 packaging work instead of Sprint 4.
- Stop if headless test coverage becomes impossible without introducing brittle UI-only tests.

## Acceptance-criteria mapping

- Directly targets the remaining gap behind the “Avalonia app shell” deliverable.
- Preserves the existing automatic daemon startup and restart-recovery foundations while planning the first real window/bootstrap step.
- Keeps Sprint 4 moving from a headless shell model toward an installable development shell connected to the real local daemon.
