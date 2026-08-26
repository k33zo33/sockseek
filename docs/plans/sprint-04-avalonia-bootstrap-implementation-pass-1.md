# Sprint 4 Avalonia bootstrap implementation pass 1

## Goal

Define the first concrete implementation pass that adds a minimal real Avalonia application bootstrap path alongside the current headless desktop shell while preserving the daemon/session/runtime seams already built during Sprint 4.

## Current-state findings

- `DesktopComposition` now centralizes desktop startup wiring and can produce both an `IDesktopProgramFlow` and the current headless `IDesktopApplicationBootstrap`.
- `DesktopComposition.CreateProgramFlow(...)` now accepts both a window lifetime and an optional shell-host factory, so future UI-specific hosts can reuse the same session/program composition instead of duplicating startup wiring.
- `DesktopProgramBootstrap` is now focused on core program/session startup only, behind the `IDesktopProgramFlow` contract.
- `HeadlessDesktopApplicationBootstrap` owns current single-instance orchestration and cleanly sits above program flow.
- `DesktopShellWindowHost` and `IDesktopShellWindowLifetime` already provide the seam where a real UI-backed window lifetime can replace the current headless `Task.Delay` loop.
- `DesktopShellWindowViewModel` already exposes the top-level title, navigation, page, backend banner, diagnostics, theme, command palette, and player-placeholder surfaces needed for a minimal shell window.
- `Sockseek.Desktop.csproj` still has no Avalonia package references or XAML/application bootstrap wiring.
- Previous attempted Avalonia scaffolding was intentionally discarded because it did not yet hook into the actual app/bootstrap path; the next implementation pass must land as a complete vertical slice, not disconnected files.

## In scope

- Define the minimal implementation needed to add Avalonia packages and project shape to `Sockseek.Desktop`.
- Define the first real Avalonia-backed `IDesktopApplicationBootstrap` / shell-host path that can reuse `DesktopComposition`.
- Define the minimal main-window/bootstrap wiring needed to surface `DesktopShellWindowViewModel` in a real `Window`.
- Identify which current headless path must remain intact so development/runtime behavior does not regress during incremental rollout.
- Identify the smallest deterministic tests or inspections that should accompany the implementation pass.

## Out of scope

- Full desktop layout/styling polish beyond a minimal shell placeholder.
- Rich command bindings, design-token resource dictionaries, or accessibility automation peers.
- Playback, downloads, onboarding, provider auth, or packaging/publishing work.
- Any server API, daemon-handshake, or SignalR recovery protocol changes.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `Directory.Packages.props`
- `docs/plans/sprint-04-avalonia-bootstrap-implementation-pass-1.md`

## API, schema and event changes

- No server API changes.
- No schema or migration changes.
- Desktop runtime composition will gain an alternate Avalonia-backed app bootstrap path while preserving the current headless path during the transition.

## Implementation sequence

1. Add centrally managed Avalonia package versions and convert `Sockseek.Desktop` to the minimal Avalonia-ready project shape required for compiled XAML/application startup.
2. Introduce a real Avalonia `Application` entrypoint (`App.axaml` / `App.axaml.cs`) plus a minimal main window (`DesktopShellMainWindow`) that binds to `DesktopShellWindowViewModel`.
3. Add an Avalonia-backed desktop application bootstrap that owns framework startup and plugs into `IDesktopProgramFlow` through the existing `IDesktopApplicationBootstrap` seam.
4. Add an Avalonia-backed shell host or window-lifetime implementation that owns main-window creation/binding and reuses `DesktopComposition.CreateProgramFlow(...)` through the injected shell-host factory rather than reimplementing session wiring.
5. Keep the current headless path available until the Avalonia bootstrap path is fully wired and reviewed, so rollback stays trivial.
6. Add focused tests for any new non-UI composition seams and rely on direct file/diff inspection for XAML/bootstrap code until a working .NET toolchain is available in this runtime.

## Testing strategy

- Preserve existing headless unit tests around `DesktopProgramBootstrap`, `DesktopComposition`, `DesktopShellWindowHost`, `DesktopShellWindowViewModel`, and shell/session behavior.
- Add focused composition tests around any new Avalonia bootstrap adapter only where behavior can be validated without running a live UI.
- Once `dotnet` is available, run at minimum:
  - `dotnet restore`
  - `dotnet build -c Release`
  - targeted desktop tests
- If Avalonia-specific build wiring cannot be validated in this runtime, explicitly treat that as an environment blocker rather than guessing.

## Migration and rollback

- No migration.
- Rollback is a revert of Avalonia package/project/bootstrap changes; the headless bootstrap path should remain available during the first implementation pass to reduce rollback risk.

## Security, privacy and license impact

- Reuses the existing localhost daemon, handshake, and bearer-token foundations.
- Does not widen secret scope or introduce new external services.
- No license impact; remains aligned with ADR-0004 and the AGPL/local-first constraints.

## Risks and stop conditions

- Stop if Avalonia startup wiring requires changing the locked process topology or moving daemon ownership out of `DesktopShellSession`.
- Stop if the minimal Avalonia project shape forces packaging/runtime decisions that belong to Sprint 14 rather than Sprint 4.
- Stop if a usable first pass would require abandoning the current headless path before the Avalonia path is actually runnable.
- Stop if XAML/build integration cannot be added confidently from docs/source inspection without a testable .NET toolchain.

## Acceptance-criteria mapping

- Directly targets the unfinished `Avalonia app shell` sprint deliverable.
- Preserves the already-built daemon startup, secure handshake, reconnect, theme, command-palette, and shell-view-model foundations while finally defining the real UI bootstrap step.
- Keeps Sprint 4 moving toward the required output artifact: a runnable development shell connected to the real local daemon.
