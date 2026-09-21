# Sprint 7 - LibVLC media engine

## Goal

Replace the placeholder player engine with a local LibVLCSharp-backed `IMediaEngine` implementation for local files.

## Current-state findings

- ADR-0007 selects LibVLCSharp for the local player engine.
- `PlaybackCoordinator` already owns state transitions and depends on `IMediaEngine`.
- `ServerHost` wires `PlaybackCoordinator` but previously left it on the default unavailable engine.
- NuGet lock files are checked in and must be updated when Player package dependencies change.

## In scope

- Add LibVLCSharp and the Windows LibVLC runtime package to the Player project.
- Implement load, play, pause, stop, seek, volume and mute in `Sockseek.Player`.
- Register the concrete engine in the daemon composition root.
- Update package locks and third-party notices.

## Out of scope

- Player HTTP API endpoints.
- Desktop bottom player UI.
- Progressive playback fixtures.
- External provider playback or audio downloading.

## Files and projects affected

- `Directory.Packages.props`
- `Sockseek.Player`
- `Sockseek.Server`
- affected `packages.lock.json` files for projects that transitively reference Player/Server
- `THIRD-PARTY-NOTICES`

## API, schema and event changes

- No HTTP API, database schema or SignalR event changes.
- Adds a concrete implementation of the existing Player `IMediaEngine` contract.

## Implementation sequence

1. Add central package versions and Player package references.
2. Implement `LibVlcMediaEngine`.
3. Register `IMediaEngine` in `ServerHost`.
4. Restore packages and update lock files.
5. Build and run tests.

## Testing strategy

- `dotnet restore`
- `dotnet build -c Release --no-restore -p:BuildInParallel=false -m:1`
- `dotnet test -c Release --no-build -p:BuildInParallel=false -m:1`

## Migration and rollback

- No database migration.
- Rollback removes the package references, lock entries, engine class and DI registration.

## Security, privacy and license impact

- Playback remains local file based through `PlaybackCoordinator` source resolution.
- No provider audio URL, provider stream or provider download capability is introduced.
- `LibVLCSharp` and `VideoLAN.LibVLC.Windows` are LGPL-2.1-or-later and are recorded in third-party notices.

## Risks and stop conditions

- Stop if the concrete engine requires changing the locked local-file-only playback policy.
- Stop if package restore introduces incompatible or non-redistributable native dependencies.
- Codec behavior is covered by the Sprint 7 local-file fixture matrix and long local playback smoke coverage; progressive codec behavior remains Sprint 8 scope.

## Acceptance-criteria mapping

- Supports local player MVP by replacing the unavailable engine with a local media engine.
- Does not complete media keys, Desktop UI or full queue restore acceptance criteria.
