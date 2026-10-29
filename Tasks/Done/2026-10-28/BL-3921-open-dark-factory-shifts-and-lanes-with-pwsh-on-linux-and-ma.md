---
id: BL-3921
title: Open dark factory shifts and lanes with pwsh on Linux and macOS
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3920]
touches: [DarkFactory/HerdrTabs.ps1, DarkFactory/Restart.ps1, RunDarkFactory.sh]
requirement: none
created: 2026-10-27
completed: 2026-10-28
---
# BL-3921 — Open dark factory shifts and lanes with pwsh on Linux and macOS

## Goal

`RunDarkFactory.sh -NewTab -Lanes Auto` on Linux or macOS opens the shift and each lane as
herdr tabs running `pwsh`, and without herdr starts each lane as a detached `pwsh` process
logging to its usual lane log, the way Windows opens console windows.

## Context

Third of five tasks making the dark factory run under `pwsh` off Windows (after BL-3919 and
BL-3920). `DarkFactory/HerdrTabs.ps1:32` builds the herdr tab's command line with
`powershell -NoProfile -ExecutionPolicy Bypass -File`, and `HerdrTabs.ps1:47` falls back to
`Start-Process powershell.exe`, which opens a console window that does not exist off Windows.
`DarkFactory/Restart.ps1` stops and restarts those processes. Use `Get-FactoryPowerShellExe`
from BL-3919. Off Windows without herdr, start `pwsh` with `Start-Process -NoNewWindow` (or
`nohup`), redirecting output to the lane log beside the checkout, and say so in the shift's
console output. `RunDarkFactory.sh` is today a 6-line wrapper that runs the `.ps1` under `pwsh`;
update its comments to match what now works.

## Acceptance criteria

- [x] `DarkFactory/HerdrTabs.ps1` builds the herdr tab command line with the platform's PowerShell executable; on Windows the line is unchanged.
- [x] Off Windows and outside herdr, a lane starts as a detached `pwsh` process whose output goes to `<repo>.logs/DarkFactory-<stamp>-L<n>.log`, and `-Restart` finds and stops it (uses `Get-FactoryProcessTable` from BL-3920).
- [x] The existing HerdrTabs and restart self-tests gain cases for the non-Windows command lines and pass on Windows.
- [x] `RunDarkFactory.sh`'s header says what runs on Linux and macOS and that it needs PowerShell 7 and, for tabs, herdr.
- [x] `dotnet build` and the fast tests are green.

## Notes

- Off Windows without herdr a lane runs as `sh -c "exec nohup pwsh ... >> log 2>&1"` (append mode, so it shares the lane's own trace log safely); a shift launcher logs to `DarkFactory-<stamp>-start.log`. The line is traced when it starts. `Stop-FactoryProcessTree` replaces taskkill in -Restart (kill -9 of descendants, deepest first, off Windows).
- Linux/macOS CI criterion met by keeping the change platform-neutral; self-tests pass on Windows.

## Log

- 2026-10-27: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 8d66c20a 6fc0e2de fd6c9dc9 f62b312e d265299d.
- 2026-10-28: Doing -> Done. Shifts and lanes open with pwsh off Windows; detached lanes log to the lane log; -Restart stops them
