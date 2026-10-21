# Package smoke

Use the Sprint 16 helper before publishing a Windows release candidate. It requires a clean worktree and writes ignored artifacts under `.tmp\sprint16-windows-package-smoke-<commit>\`.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\run_windows_package_smoke.ps1 -Force
```

The helper records the full checked-out commit and rejects a dirty worktree or a
different requested commit. It runs the same publish, SBOM, staging and archive
steps shown below, then extracts the exact ZIP and verifies:

- the packaged daemon returns a healthy loopback `/health` response;
- the protected `/api/v1/system/info` endpoint rejects an anonymous request;
- the packaged daemon completes authenticated Soulseek account configure, process restart/restore and disconnect without returning plaintext or leaving a secret payload behind;
- the daemon has no non-loopback TCP listener;
- the packaged Desktop starts its packaged child daemon from the extracted archive;
- the Desktop child daemon is healthy, protected and loopback-only; and
- cleanup is limited to the exact smoke process IDs and isolated smoke data directories.

The helper writes `package-smoke-evidence.json` with the full commit, archive hash,
runtime, host Windows caption/version/build/architecture and passed runtime checks.
Keep that file, the console output and generated SHA256 manifest with the
release-candidate evidence. Host metadata is evidence, not a target-OS substitute:
a run on Windows 10 does not satisfy the Windows 11 gate.

Run the fast tester-instructions smoke when changing beta limitation, diagnostics, security or tester-instruction text:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\test_closed_beta_tester_instructions.ps1
```

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
- `SECURITY.md`
- `docs/beta-limitations.md`
- `closed-beta-tester-instructions.md`
- `release-metadata.json`
- `sbom.spdx.json`
- `install.ps1`
- `uninstall.ps1`

The archive command creates a Windows release zip plus a SHA256 manifest for the archive and every staged file.

## Latest local smoke results

Recorded on 2026-10-13 for commit `9a7db5a` with `scripts\run_windows_package_smoke.ps1 -Force`:

- `dotnet publish Sockseek.Desktop\Sockseek.Desktop.csproj -c Release -r win-x64 --self-contained true` passed in the helper.
- `dotnet publish Sockseek.Server\Sockseek.Server.csproj -c Release -r win-x64 --self-contained true` passed in the helper.
- `dotnet run --project Sockseek.Packager\Sockseek.Packager.csproj -c Release -- generate-sbom ...` generated an SPDX SBOM with 126 packages.
- `dotnet run --project Sockseek.Packager\Sockseek.Packager.csproj -c Release -- stage-windows ...` passed staging validation.
- `dotnet run --project Sockseek.Packager\Sockseek.Packager.csproj -c Release -- archive-windows ...` created `Sockseek-win-x64.zip` and `Sockseek-win-x64.sha256`.
- `closed-beta-tester-instructions.md` was generated into the staged payload and identified commit `9a7db5a`, closed/internal-only status, legal-use guidance, diagnostics redaction guidance and the current Sprint 15 limitations.
- `Sockseek-win-x64.sha256` listed `SECURITY.md`, `THIRD-PARTY-NOTICES`, `docs/beta-limitations.md` and `closed-beta-tester-instructions.md`.
- `release-metadata.json` identified version `3.0.5`, commit `9a7db5a`, source URL `https://github.com/k33zo33/sockseek` and license `AGPL-3.0`.
- The staged daemon output included `e_sqlite3.dll` and `libvlc` native assets.
- Local publish, staging validation, SBOM generation and archive creation completed without the earlier `NU1900` package-vulnerability-source warning; the separate dependency vulnerability scan gate remains recorded in `docs/beta-go-no-go.md`.

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

Sprint 15 smoke artifacts recorded above were written under `.tmp\sprint15-package-smoke-9a7db5a\`, which is ignored by Git.

## Ubuntu 24.04 x64 package smoke

Run the Sprint 16 helper from a clean worktree with Docker Desktop using Linux containers:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\run_ubuntu_package_smoke.ps1
```

The helper records the full checked-out commit and rejects a dirty worktree. It then:

- publishes the self-contained `linux-x64` Desktop and daemon inside the .NET 10 Ubuntu Noble SDK image, with all container build intermediates isolated under the ignored smoke artifact root;
- generates the SPDX SBOM, Linux staging layout, closed-beta tester instructions, `tar.gz` archive and SHA256 manifest;
- builds a pinned Ubuntu 24.04 runtime image with the required Avalonia and LibVLC native dependencies;
- extracts the exact archive and verifies required legal/security files and executable modes;
- starts the packaged daemon and requires a healthy loopback `/health` response;
- verifies anonymous API rejection plus packaged encrypted Soulseek account configure, process restart/restore and disconnect, including `0700` secret-directory and `0600` key/payload modes and cleanup;
- starts the packaged Desktop under Xvfb and requires its packaged local daemon to become healthy; and
- exercises the packaged user-local `install.sh` and `uninstall.sh` while confirming user data is preserved by default.

Artifacts are written under `.tmp\sprint16-ubuntu-package-smoke-<commit>\`. Keep the archive hash, manifest and console transcript with release-candidate evidence. This Ubuntu smoke is distinct from the Alpine Docker/headless path documented in `docs/docker.md`.

The Ubuntu package expects the system LibVLC runtime and unversioned loader alias provided by `vlc` and `libvlc-dev`. The packaged `install.sh` checks this prerequisite and prints the exact `apt` command when it is absent.

## Sprint 16 exact-candidate evidence

Candidate `422a001f5e6275cb312c7c42d954b8e58a0c6d2a` was exercised on
2026-10-21:

- Ubuntu 24.04 x64 exact-package smoke passed publish, 132-package SBOM,
  staging, tar extraction, daemon health, authenticated encrypted Soulseek
  account configure, daemon restart/restore and disconnect, Unix `0700`/`0600` secret permissions,
  Desktop child-daemon health under Xvfb, install/uninstall and default user-data
  preservation. The archive is
  `Sockseek-linux-x64.tar.gz`, SHA256
  `6474ed5b7b4be3370d315a001f823cbab7af0af50179cabceb49e6840ec3b287`.
- Windows `win-x64` exact-package preflight passed publish, 132-package SBOM,
  staging, ZIP extraction, direct daemon health/auth/loopback checks and
  packaged Desktop child-daemon health/auth/loopback checks. The direct daemon
  also passed DPAPI-backed Soulseek account configure, daemon restart/restore
  and disconnect with response/plaintext checks and secret cleanup. The archive is
  `Sockseek-win-x64.zip`, SHA256
  `b7a5349c424202058bfeff140b34261de37830c2565c7af189074e180d445973`.
- The Windows evidence JSON identifies Microsoft Windows 10 Pro
  `10.0.19045`, 64-bit. This is not the required Windows 11 x64 acceptance
  run.

The complete gate result and remaining blockers are recorded in
`docs/sprint-16-release-candidate-evidence.md`.
