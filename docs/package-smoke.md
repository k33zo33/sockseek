# Package smoke

Use the Sprint 15 helper before publishing a Windows release candidate. It assumes a clean worktree and writes ignored artifacts under `.tmp\sprint15-package-smoke-<commit>\`.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\run_windows_package_smoke.ps1 -Force
```

The helper runs the same publish, SBOM, staging and archive steps shown below. Keep the console output and generated SHA256 manifest with the release-candidate evidence.

Manual equivalent:

```powershell
$commit = git rev-parse --short HEAD

dotnet publish Sockseek.Desktop\Sockseek.Desktop.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o .tmp\publish-desktop-win-x64

dotnet publish Sockseek.Server\Sockseek.Server.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o .tmp\publish-daemon-win-x64

dotnet run --project Sockseek.Packager\Sockseek.Packager.csproj -c Release -- `
  generate-sbom `
  . `
  .tmp\release-sbom.spdx.json `
  Sockseek `
  3.0.5 `
  $commit `
  https://github.com/k33zo33/sockseek

dotnet run --project Sockseek.Packager\Sockseek.Packager.csproj -c Release -- `
  stage-windows `
  . `
  .tmp\publish-desktop-win-x64 `
  .tmp\publish-daemon-win-x64 `
  .tmp\stage-win-x64 `
  Sockseek.Desktop.exe `
  Sockseek.Server.exe `
  3.0.5 `
  $commit `
  https://github.com/k33zo33/sockseek `
  .tmp\release-sbom.spdx.json

dotnet run --project Sockseek.Packager\Sockseek.Packager.csproj -c Release -- `
  archive-windows `
  .tmp\stage-win-x64 `
  .tmp\Sockseek-win-x64.zip `
  .tmp\Sockseek-win-x64.sha256 `
  Sockseek.Desktop.exe `
  Sockseek.Server.exe
```

The staging command validates that the staging directory contains:

- `Sockseek.Desktop.exe`
- `daemon/Sockseek.Server.exe`
- `LICENSE`
- `THIRD-PARTY-NOTICES`
- `release-metadata.json`
- `sbom.spdx.json`
- `install.ps1`
- `uninstall.ps1`

The archive command creates a Windows release zip plus a SHA256 manifest for the archive and every staged file.

## Latest local smoke results

Recorded on 2026-10-10 for commit `2c4cdc8` with `scripts\run_windows_package_smoke.ps1 -Force`:

- `dotnet publish Sockseek.Desktop\Sockseek.Desktop.csproj -c Release -r win-x64 --self-contained true` passed in the helper.
- `dotnet publish Sockseek.Server\Sockseek.Server.csproj -c Release -r win-x64 --self-contained true` passed in the helper.
- `dotnet run --project Sockseek.Packager\Sockseek.Packager.csproj -c Release -- generate-sbom ...` generated an SPDX SBOM with 126 packages.
- `dotnet run --project Sockseek.Packager\Sockseek.Packager.csproj -c Release -- stage-windows ...` passed staging validation.
- `dotnet run --project Sockseek.Packager\Sockseek.Packager.csproj -c Release -- archive-windows ...` created `Sockseek-win-x64.zip` and `Sockseek-win-x64.sha256`.
- `closed-beta-tester-instructions.md` was generated into the staged payload and identified commit `2c4cdc8`, closed/internal-only status and the current Docker pipe/config diagnostics limitation.
- `Sockseek-win-x64.sha256` listed `SECURITY.md`, `THIRD-PARTY-NOTICES`, `docs/beta-limitations.md` and `closed-beta-tester-instructions.md`.
- `release-metadata.json` identified version `3.0.5`, commit `2c4cdc8`, source URL `https://github.com/k33zo33/sockseek` and license `AGPL-3.0`.
- The staged daemon output included `e_sqlite3.dll` and `libvlc` native assets.
- Local publish, staging validation, SBOM generation and archive creation completed. The publish steps emitted `NU1900` package-vulnerability-source warnings because `https://api.nuget.org/v3/index.json` was unavailable during this run; use the separate dependency vulnerability scan gate for vulnerability-source evidence.

For the Sprint 14 Windows staging smoke, the generated SBOM contained 126 packages and the staging validator passed. The staged installer creates user-level install and data directories; the uninstaller preserves user data unless `-RemoveUserData` is passed.

Clean local smoke artifacts after review:

```powershell
Remove-Item -LiteralPath `
  .tmp\publish-desktop-win-x64, `
  .tmp\publish-daemon-win-x64, `
  .tmp\stage-win-x64, `
  .tmp\Sockseek-win-x64.zip, `
  .tmp\Sockseek-win-x64.sha256, `
  .tmp\release-sbom.spdx.json `
  -Recurse -Force
```

Sprint 15 smoke artifacts recorded above were written under `.tmp\sprint15-package-smoke-2c4cdc8\`, which is ignored by Git.
