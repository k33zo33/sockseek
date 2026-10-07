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

## Optional capture helper

Use `scripts/capture_desktop_virtualization_trace.ps1` after the packaged Desktop process is running to collect consistent process metrics while the operator performs the rendered interaction steps.

Example:

```powershell
.\scripts\capture_desktop_virtualization_trace.ps1 `
  -Surface search `
  -FixtureSize 10000 `
  -DurationSeconds 180 `
  -OutputPath artifacts\desktop-virtualization-search.md `
  -SamplesCsvPath artifacts\desktop-virtualization-search.samples.csv `
  -Notes "none observed"
```

Run one capture for each required surface: `library`, `search` and `playlist`. The helper writes a Markdown report and raw CSV samples under `artifacts/` by default, which is ignored by Git; copy the summarized metrics into this document or `docs/beta-go-no-go.md` only after the trace has actually been captured on the target public-beta OS and hardware class.

If local execution policy blocks unsigned scripts, run the same command through `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\capture_desktop_virtualization_trace.ps1 ...` without changing the machine-wide policy.

## Current automated guards

- `DesktopLibraryViewModelTests.LibraryTrackList_UsesBoundedListBox`
- `DesktopSearchViewModelTests.LargeResultLists_UseBoundedListBoxes`
- `DesktopSearchViewModelTests.RefreshResultsAsync_TrackMode_MapsTenThousandSoulseekResultsWithinBudget`
- `DesktopPlaylistsViewModelTests.PlaylistsSurface_BindsListDetailAndCommands`
- `DesktopPlaylistsViewModelTests.SelectedPlaylistItems_FiltersTenThousandItemsWithinBudget`

These guards are necessary but not sufficient for public beta. Update `docs/beta-go-no-go.md` with the captured trace result before changing the public beta decision.
