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
completed:
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

- [ ] `DarkFactory/HerdrTabs.ps1` builds the herdr tab command line with the platform's PowerShell executable; on Windows the line is unchanged.
- [ ] Off Windows and outside herdr, a lane starts as a detached `pwsh` process whose output goes to `<repo>.logs/DarkFactory-<stamp>-L<n>.log`, and `-Restart` finds and stops it (uses `Get-FactoryProcessTable` from BL-3920).
- [ ] The existing HerdrTabs and restart self-tests gain cases for the non-Windows command lines and pass on Windows.
- [ ] `RunDarkFactory.sh`'s header says what runs on Linux and macOS and that it needs PowerShell 7 and, for tabs, herdr.
- [ ] `dotnet build` and the fast tests are green.

## Notes

## Log

- 2026-10-27: Created.
