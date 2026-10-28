---
id: BL-3920
title: Read memory and the process table through the platform seam so lane sizing and restarts work on Linux and macOS
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3919]
touches: [DarkFactory/Platform.ps1, DarkFactory/MachineProbe.ps1, DarkFactory/AutoLanesState.ps1, DarkFactory/LaneState.ps1, DarkFactory/Restart.ps1, DarkFactory/HerdrTabs.ps1]
requirement: none
created: 2026-10-27
completed:
---
# BL-3920 — Read memory and the process table through the platform seam so lane sizing and restarts work on Linux and macOS

## Goal

The dark factory reads total and free memory and the process table (process id, parent id,
command line) through helpers in `DarkFactory/Platform.ps1`, so `-Lanes Auto`, the machine
probe, lane liveness and `-Restart` work on Linux and macOS as well as Windows.

## Context

Second of five tasks making the dark factory run under `pwsh` off Windows (BL-3919 adds the
seam). `Get-CimInstance` and `Win32_*` classes exist only on Windows:
- `DarkFactory/AutoLanesState.ps1:31` and `DarkFactory/MachineProbe.ps1:71, 88, 123`: total and free
  memory (`Win32_ComputerSystem`, `Win32_OperatingSystem`).
- `DarkFactory/HerdrTabs.ps1:157` and `DarkFactory/Restart.ps1:53`: the process table with command
  lines (`Win32_Process`).
- `DarkFactory/LaneState.ps1:59` and `DarkFactory/Restart.ps1:73`: `Get-Process` works everywhere, but
  `Restart.ps1:73` matches the process name `powershell`, which is `pwsh` off Windows.

Off Windows: Linux reads `/proc/meminfo` (MemTotal, MemAvailable) and `/proc/<pid>/cmdline`
plus `/proc/<pid>/stat` (or `ps -axo pid=,ppid=,args=`); macOS uses `sysctl -n hw.memsize`,
`vm_stat` for free pages, and `ps -axo pid=,ppid=,args=`. Keep Windows behaviour the same.

## Acceptance criteria

- [ ] `DarkFactory/Platform.ps1` defines `Get-FactoryTotalMemoryBytes`, `Get-FactoryFreeMemoryBytes` and `Get-FactoryProcessTable` (objects with `ProcessId`, `ParentProcessId`, `CommandLine`), each with a Windows branch that returns what the old `Get-CimInstance` calls returned and Linux and macOS branches as described in Context.
- [ ] `grep -n "Get-CimInstance" DarkFactory/*.ps1` matches only inside `DarkFactory/Platform.ps1`.
- [ ] `DarkFactory/Restart.ps1` recognises a lane process named `powershell` or `pwsh`.
- [ ] `RunDarkFactory.ps1 -TestPlatform` also checks the three new helpers (memory above zero, free not above total, the current process found in the table with its parent), and passes on Windows.
- [ ] `-TestMachineProbe`, `-TestAutoLanes` and the restart self-test still pass on Windows, and `dotnet build` plus the fast tests are green.

## Notes

## Log

- 2026-10-27: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: ea931375 9d75da91 70c1db98 96f55cf5 c112e270.
