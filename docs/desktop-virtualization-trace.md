# Desktop virtualization trace checklist

Sprint 15 requires a rendered UI trace before public beta. XAML and view-model tests prove that the large library, search and playlist item collections are bounded and explicitly use virtualized item panels, but they do not prove rendered scroll behavior or native UI memory stability.

## Required trace

Capture a trace on the target public-beta OS and hardware class:

| Surface | Fixture | Pass condition |
| --- | --- | --- |
| Library tracks | 100,000 indexed canonical tracks, first result page loaded | Initial render remains responsive, scrolling does not allocate unbounded row controls, no text overlap. |
| Search file candidates | 10,000 Soulseek file candidates loaded into the Search view | Initial render remains responsive, scrolling keeps memory bounded, download buttons remain reachable. |
| Playlist items | 10,000 imported playlist items loaded into the Playlists view | Filter changes remain under budget, scrolling keeps memory bounded, row actions remain usable. |

## Suggested capture steps

1. Build Release and run the Desktop app from the same commit being evaluated.
2. Start the local daemon through the packaged Desktop flow, not from a developer-only replacement host.
3. Load each fixture and navigate to the corresponding view.
4. Capture process private memory, managed heap if available, CPU spikes and frame responsiveness while:
   - opening the view;
   - scrolling from top to bottom and back;
   - changing playlist filters and search text;
   - resizing the window between narrow and wide desktop sizes.
5. Save the trace summary with:
   - commit SHA;
   - OS and version;
   - display scaling;
   - fixture size;
   - peak private memory delta;
   - observed UI defects or "none observed".

## Optional fixture preparation

Use `scripts/prepare_desktop_virtualization_fixture.ps1` to prepare an isolated local fixture before launching the rendered Desktop app. The script creates:

- a migrated SQLite config directory with 100,000 library tracks;
- a 10,000 item imported playlist;
- a 10,000 file mock Soulseek tree that can be searched without real Soulseek network access.

Example:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\prepare_desktop_virtualization_fixture.ps1 -Force
```

The script writes a `desktop-trace-fixture-readme.md` under `.tmp\sprint15-desktop-trace-fixture\` with the exact launch environment. Start Desktop from the same shell after setting:

```powershell
$env:SOCKSEEK_DESKTOP_CONFIG_DIR = ".tmp\sprint15-desktop-trace-fixture\config"
$env:SockseekServer__Engine__MockFilesDir = ".tmp\sprint15-desktop-trace-fixture\mock-soulseek"
$env:SockseekServer__Engine__MockFilesReadTags = "false"
dotnet run --project Sockseek.Desktop\Sockseek.Desktop.csproj -c Release
```

Before launching the rendered app for public-beta evidence, run the fixture preflight. It verifies the fixture paths, database file, mock Soulseek file count, packaged Desktop executable path when supplied and, when `sqlite3` is available, the required library and playlist row counts:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\validate_desktop_virtualization_fixture.ps1 `
  -DesktopExecutablePath ".tmp\sprint15-package-smoke-<commit>\stage-win-x64\Sockseek.Desktop\Sockseek.Desktop.exe" `
  -RequireSqliteRowCounts
```

If `sqlite3` is not on `PATH`, pass `-SqlitePath` with the executable location or omit `-RequireSqliteRowCounts` for a structural rehearsal-only check. A structural check is useful for setup debugging, but public-beta trace evidence should verify row counts before capture.

The fixture writer is opt-in and covered by `DesktopVirtualizationTraceFixtureWriterTests`; default test runs skip database generation unless `SOCKSEEK_WRITE_DESKTOP_TRACE_FIXTURE=1` is set. Fixture preparation does not satisfy the rendered UI trace gate by itself.

To launch Desktop with the fixture environment consistently, use:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\start_desktop_virtualization_fixture.ps1
```

By default the launch helper uses `dotnet run --project Sockseek.Desktop\Sockseek.Desktop.csproj -c Release --no-build`, which is useful for rehearsal. For public-beta rendered trace evidence, pass `-DesktopExecutablePath` pointing at the packaged Desktop executable being evaluated so the trace covers the shipped artifact.

## Optional capture helper

Use `scripts/capture_desktop_virtualization_trace.ps1` after the packaged Desktop process is running to collect consistent process metrics while the operator performs the rendered interaction steps.

Example:

```powershell
.\scripts\capture_desktop_virtualization_trace.ps1 `
  -Surface search `
  -FixtureSize 10000 `
  -DurationSeconds 180 `
  -DisplayScaling "100%" `
  -OperatorActions "Loaded the Search view with the 10,000 candidate fixture; scrolled top-to-bottom and back; resized narrow/wide; checked download buttons and text overlap." `
  -OutputPath artifacts\desktop-virtualization-search.md `
  -SamplesCsvPath artifacts\desktop-virtualization-search.samples.csv `
  -Notes "none observed"
```

Run one capture for each required surface: `library`, `search` and `playlist`. The helper writes a Markdown report and raw CSV samples under `artifacts/` by default, which is ignored by Git; copy the summarized metrics into this document or `docs/beta-go-no-go.md` only after the trace has actually been captured on the target public-beta OS and hardware class.

If local execution policy blocks unsigned scripts, run the same command through `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\capture_desktop_virtualization_trace.ps1 ...` without changing the machine-wide policy.

To run the three captures in sequence and validate them immediately after the last capture, use:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\run_desktop_virtualization_trace_capture.ps1 `
  -DurationSeconds 180 `
  -DisplayScaling "100%" `
  -OperatorActions "Loaded each requested fixture surface; scrolled top-to-bottom and back; resized narrow/wide; checked row actions and text overlap." `
  -OutputDirectory artifacts
```

The runner pauses before each surface unless `-SkipPrompts` is passed. Load the requested fixture in the rendered Desktop window before continuing each capture. The runner does not generate fixture data and does not replace the operator check for visible defects.

The helper now requires the target process to have a main window by default so a headless or background process cannot be mistaken for rendered UI evidence. It records both `Main window present` and `Headless process allowed` in each report. `-AllowHeadlessProcess` is only for validating the harness itself; reports captured with it are rejected by the validator and do not satisfy the public-beta rendered trace gate.

After all three captures are complete, validate the captured reports before recording the gate result:

```powershell
.\scripts\validate_desktop_virtualization_traces.ps1 `
  -LibraryReportPath artifacts\desktop-virtualization-library.md `
  -SearchReportPath artifacts\desktop-virtualization-search.md `
  -PlaylistReportPath artifacts\desktop-virtualization-playlist.md
```

The helper attempts to detect Windows display scaling automatically. If the report records `unknown - rerun with -DisplayScaling`, rerun the capture with the concrete scaling value from Windows display settings, for example `-DisplayScaling "125%"`.

The validator checks that all three surfaces are present, use the same concrete commit, meet the required fixture sizes, record concrete display scaling, record explicit operator actions and reference existing raw sample CSV files. Passing validation still does not replace the manual operator check for visible text overlap, row actions and responsiveness; record those observations in each capture's notes.

Use `scripts/test_desktop_virtualization_trace_validator.ps1` to smoke-test the validator itself. The smoke creates synthetic valid and invalid reports, including headless, undersized fixture and mixed-commit cases; it does not satisfy the rendered UI trace gate. Use `scripts/test_desktop_virtualization_trace_capture_runner.ps1` to smoke-test the three-surface runner without launching the Desktop app.
Use `scripts/test_desktop_virtualization_fixture_preflight.ps1` to smoke-test fixture preflight validation without creating the full 100k/10k fixture.

## Current automated guards

- `DesktopLibraryViewModelTests.LibraryTrackList_UsesBoundedListBox`
- `DesktopSearchViewModelTests.LargeResultLists_UseBoundedListBoxes`
- `DesktopSearchViewModelTests.RefreshResultsAsync_TrackMode_MapsTenThousandSoulseekResultsWithinBudget`
- `DesktopPlaylistsViewModelTests.PlaylistsSurface_BindsListDetailAndCommands`
- `DesktopPlaylistsViewModelTests.SelectedPlaylistItems_FiltersTenThousandItemsWithinBudget`

These guards are necessary but not sufficient for public beta. Update `docs/beta-go-no-go.md` with the captured trace result before changing the public beta decision.
