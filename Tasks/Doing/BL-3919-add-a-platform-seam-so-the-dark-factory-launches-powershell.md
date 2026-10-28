---
id: BL-3919
title: Add a platform seam so the dark factory launches PowerShell and finds its data folder on Linux and macOS
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/Platform.ps1, RunDarkFactory.ps1, DarkFactory/CiWatch.ps1, DarkFactory/AuditCadence.ps1, DarkFactory/ShiftEnd.ps1, DarkFactory/Prompts.ps1, DarkFactory/AutoLanesState.ps1, DarkFactory/LaneRules.md]
requirement: none
created: 2026-10-27
completed:
---
# BL-3919 — Add a platform seam so the dark factory launches PowerShell and finds its data folder on Linux and macOS

## Goal

Every place the dark factory starts a child PowerShell or reads its per-user data folder
goes through one new area file, `DarkFactory/Platform.ps1`, which picks `powershell` on
Windows and `pwsh` on Linux and macOS, so the factory can run under PowerShell 7 off Windows.

## Context

Stewart asked (2026-10-27) for the dark factory to run on Unix. The root `.ps1` scripts got
bash siblings that day, and `RunDarkFactory.sh` runs `RunDarkFactory.ps1` under `pwsh`, but
the factory's code assumes Windows and none of the 32 area files checks `$IsWindows`. This is
the first of five tasks (with BL-3920 to BL-3923); the others build on this seam.

Windows-only spots this task covers:
- Hard-coded `powershell -NoProfile -ExecutionPolicy Bypass -File ...` calls: `DarkFactory/CiWatch.ps1`
  (lines ~311, 586, 666, 740-749, 985, including a `Start-Process powershell.exe`),
  `DarkFactory/AuditCadence.ps1:15`, `DarkFactory/AutoLanesState.ps1:71`, `DarkFactory/ShiftEnd.ps1:130`.
- Prompt text that tells a lane to run `powershell ...`: `DarkFactory/Prompts.ps1:461`,
  `DarkFactory/ShiftEnd.ps1:113`, and the same instruction in `DarkFactory/LaneRules.md`.
- `%LOCALAPPDATA%` paths (the audio-off file is read in `Alarm.ps1:168`; BL-3922 switches that
  one to the helper).

Keep Windows behaviour byte-for-byte the same. Off Windows, the data folder is
`$XDG_DATA_HOME/Curl` (falling back to `~/.local/share/Curl`) on Linux and
`~/Library/Application Support/Curl` on macOS, the same rule `Set-DifferentialEnvironment.sh` uses.

## Acceptance criteria

- [ ] `DarkFactory/Platform.ps1` exists, is dot-sourced by `RunDarkFactory.ps1`'s loader before every file that uses it, and defines `Get-FactoryPowerShellExe` (returns `powershell` when `$IsWindows` or on Windows PowerShell 5.1, `pwsh` otherwise) and `Get-FactoryDataDir` (returns `%LOCALAPPDATA%\Curl` on Windows and the Linux/macOS folders named in Context).
- [ ] `grep -nE "& powershell |'powershell\.exe'" DarkFactory/*.ps1` finds no call in `CiWatch.ps1`, `AuditCadence.ps1`, `AutoLanesState.ps1` or `ShiftEnd.ps1`; each now runs `& (Get-FactoryPowerShellExe) ...`.
- [ ] The lane-facing instructions in `DarkFactory/Prompts.ps1`, `DarkFactory/ShiftEnd.ps1` and `DarkFactory/LaneRules.md` name the PowerShell executable for the platform the shift runs on (or say "`powershell` on Windows, `pwsh` elsewhere").
- [ ] `RunDarkFactory.ps1` gains a `-TestPlatform` self-test, listed in its help examples, that checks both helpers' answers for the current OS and exits 0 on Windows.
- [ ] Every existing `-Test*` self-test that touches a changed file still passes on Windows (`-TestCiWatch`, `-TestAutoLanes`, and the audit-cadence check), and `dotnet build` plus the fast tests are green.

## Notes

## Log

- 2026-10-27: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 9aafd54f 7ccad5a0 bc1d9518 a0bb6b24 122c5056.
