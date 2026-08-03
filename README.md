# diskscape

**Windows disk-space visualizer** — scan any folder or drive fast, see exactly what's eating your space with an interactive **treemap / sunburst**, drill down to the biggest offenders, and clean up safely. Offline by default, privacy-first. Optional local-AI cleanup suggestions via tiny models.

> Status: 🚧 Early scaffold. See [PLAN.md](PLAN.md) and the issue backlog.

## Overview

diskscape answers the question every Windows user eventually asks: *"Where did all my disk space go?"* It performs a fast, parallel scan of a chosen drive or folder, aggregates size by folder/file, and renders it as an interactive **treemap** (and optional **sunburst**) where the biggest rectangles are the biggest space hogs. Click to zoom in, right-click to open in Explorer or delete to the Recycle Bin.

Everything runs locally. No files leave your machine and no cloud account is required for the core value.

## Motivation

- Built-in Windows Storage Settings is slow and shallow; it won't show you the one 40 GB folder buried five levels deep.
- Classic tools (WinDirStat) are effective but dated and slow on large NTFS volumes.
- People want a **fast, modern, privacy-respecting** visualizer that also offers *smart* (optional, local) suggestions about what is safe to clean.

## Use cases

- "My C: drive is full" — find the top space consumers in seconds.
- Locate giant log files, orphaned installer caches, node_modules graveyards, old ISO/VM images.
- Compare folder sizes at a glance before archiving or moving to another drive.
- Clean up safely (Recycle Bin by default) with an undo-friendly workflow.
- Export a size report (CSV/JSON) for later review.

## How to use (Windows-first quickstart)

> Requires Windows 10/11 (x64) and the .NET 8 Desktop Runtime (bundled in the self-contained build).

1. Download the latest `diskscape-win-x64.zip` from Releases (or build from source — see below).
2. Unzip and run `diskscape.exe`.
3. Pick a drive (e.g. `C:\`) or **Browse…** to a folder, then click **Scan**.
4. Watch the treemap fill in. Bigger tile = more space. Click a tile to zoom; breadcrumb to go back.
5. Right-click any tile → **Open in Explorer**, **Copy path**, or **Delete (Recycle Bin)**.
6. Optionally toggle the **Sunburst** view or export a report.

### Build from source

```powershell
git clone https://github.com/rwrife/diskscape.git
cd diskscape
dotnet build -c Release
dotnet run --project src/DiskScape.App
```

## Example workflow

```
1. Launch diskscape → select C:\ → Scan
2. Treemap shows Users\me\AppData is huge (18 GB)
3. Zoom into AppData → Local\Temp is 6 GB
4. Right-click Temp → Delete (Recycle Bin)
5. Re-scan that node → space reclaimed
6. File → Export report → disk-report.csv
```

## Local-AI integration (optional)

diskscape works fully without AI. When enabled, it can talk to a **local** OpenAI-compatible endpoint (e.g. [Ollama](https://ollama.com) or [llama.cpp](https://github.com/ggerganov/llama.cpp)) running a small model (Llama 3.2 1B/3B, Qwen2.5, Phi-3-mini, MiniCPM-family) to:

- Summarize a scan ("most of your space is dev caches and installers").
- Suggest which large folders are typically **safe to clean** (temp, package caches, old logs) vs. risky.
- Explain what an unfamiliar large folder likely is.

It never sends file *contents* — only folder names, sizes, and extensions — and only when you explicitly enable it. A reachability probe checks the endpoint first and the app falls back to non-AI mode gracefully. Off by default.

## Current status / milestones

- [ ] M1 — Fast scan engine (`DiskScape.Core`) with parallel enumeration + size aggregation
- [ ] M2 — Treemap rendering + zoom/drill-down UI
- [ ] M3 — Safe cleanup actions (Recycle Bin, open in Explorer) + report export
- [ ] M4 — Sunburst view + polish
- [ ] M5 — Optional local-AI suggestions
- [ ] M6 — Windows packaging (portable zip + MSIX) & CI

See open [issues](https://github.com/rwrife/diskscape/issues) for the actionable backlog.

## License

MIT (planned).
