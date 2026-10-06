# Package smoke

Use these commands before publishing a Windows release candidate. They assume a clean worktree and run from the repository root.

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

For the Sprint 14 Windows staging smoke, the generated SBOM contained 126 packages and the staging validator passed. The staged daemon output included `e_sqlite3.dll` and `libvlc` native assets. The staged installer creates user-level install and data directories; the uninstaller preserves user data unless `-RemoveUserData` is passed.

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
