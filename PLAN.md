# diskscape — Project Plan

## Scope

A focused Windows desktop utility that scans a drive/folder and visualizes disk usage as an interactive treemap (and sunburst), with safe cleanup actions and optional local-AI suggestions.

**In scope**
- Fast, parallel directory scan with size aggregation (folders + files).
- Interactive treemap with zoom/drill-down and a details/list panel.
- Sunburst alternative view.
- Safe cleanup: delete to Recycle Bin, open in Explorer, copy path.
- Report export (CSV/JSON).
- Optional local-AI cleanup suggestions via a local OpenAI-compatible endpoint.
- Persist recent scan roots and settings.

**Out of scope (non-goals)**
- Cloud sync, accounts, telemetry.
- Cross-platform GUI (Windows-first; core library stays UI-agnostic).
- Duplicate-file detection (that's `dupe-sweeper`).
- File search / indexing (that's `file-lantern`).
- Permanent/forced deletion by default, disk defrag, or partition editing.

## Architecture / tech approach

- **Language/runtime:** .NET 8, C#.
- **UI:** WPF (MVVM). Treemap rendered on a `Canvas`/`DrawingVisual` (custom squarified-treemap layout); sunburst via WPF geometry.
- **Core library:** `DiskScape.Core` — no UI dependencies, fully unit-testable.
  - Scanner: parallel `System.IO.Enumeration.FileSystemEnumerator` for speed; builds an in-memory size tree (`ScanNode`).
  - Aggregation: post-order sum of file sizes into folder totals; track file count, largest files.
  - Progress reporting via `IProgress<ScanProgress>`; cancellation via `CancellationToken`.
  - Actions: Recycle Bin delete (via `Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile` with `RecycleOption.SendToRecycleBin` or SHFileOperation P/Invoke), open-in-Explorer.
  - Report export: CSV/JSON serializers.
  - Optional AI: `IAiSuggester` calling a local OpenAI-compatible `/v1/chat/completions` endpoint with a reachability probe + graceful fallback; sends only names/sizes/extensions, never file contents.
- **Layout algorithm:** squarified treemap for good aspect ratios; incremental re-layout on zoom.
- **Storage/settings:** JSON config under `%APPDATA%\diskscape` (recent roots, AI endpoint, view prefs).
- **Testing:** xUnit against `DiskScape.Core` (scanner on temp fixtures, aggregation math, treemap layout invariants, CSV/JSON export, AI fallback logic).

### Project layout (planned)
```
src/
  DiskScape.Core/     # scanning, aggregation, actions, export, AI client
  DiskScape.App/      # WPF UI (treemap, sunburst, panels)
tests/
  DiskScape.Core.Tests/
```

## Milestones

1. **M1 — Scan engine:** `DiskScape.Core` parallel scan → size tree with progress + cancellation; xUnit coverage.
2. **M2 — Treemap UI:** squarified treemap render, zoom/drill-down, breadcrumb, details panel.
3. **M3 — Cleanup + export:** Recycle Bin delete, open in Explorer, copy path, CSV/JSON export.
4. **M4 — Sunburst + polish:** sunburst view toggle, color-by-type, keyboard nav, DPI awareness.
5. **M5 — Local-AI:** optional suggester with endpoint probe + graceful fallback (off by default).
6. **M6 — Packaging/CI:** self-contained win-x64 portable zip + MSIX; GitHub Actions CI on `windows-latest`.

## Packaging target for Windows

- Primary: **portable self-contained win-x64 zip** (no runtime install needed).
- Secondary: **MSIX** installer for Start-menu integration.
- CI: GitHub Actions on `windows-latest` — build, run `DiskScape.Core` tests, publish artifacts on tagged releases.
