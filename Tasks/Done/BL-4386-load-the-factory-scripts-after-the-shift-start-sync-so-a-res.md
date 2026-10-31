---
id: BL-4386
title: Load the factory scripts after the shift-start sync, so a restart after a push runs the pushed code
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [DarkFactory/ShiftCoordinator.ps1, DarkFactory/AffectedTests.ps1, DarkFactory/Restart.ps1]
requirement: none
created: 2026-10-31
completed: 2026-10-31
---
# BL-4386 — Load the factory scripts after the shift-start sync, so a restart after a push runs the pushed code

## Goal

A shift whose start-up sync fast-forwards the checkout runs the code it fast-forwarded to, so `RunDarkFactory.cmd -Restart` straight after a push to the shift branch puts that push into effect.

## Context

The coordinator dot-sources `RunDarkFactory.ps1` and the `DarkFactory/` area files when its process starts, and only then calls `Sync-CheckoutWithOrigin` (`DarkFactory/ShiftCoordinator.ps1` near line 119, `DarkFactory/AffectedTests.ps1` line 9), which fast-forwards the checkout. 2026-10-31: 8c14126f5 (BL-4384's CI-watch fix) was pushed at about 06:03; `-Restart` started coordinator pid 57516 at 06:05:44, its sync fast-forwarded to 8c14126 at 06:05:46, and DarkFactory-20261031-060544.log still wrote the old bare `cannot list the CI runs ... with gh` at 06:06:43. That shift also split BL-4377 for its cost cap before the fix ran. A second `-Restart` (06:08:33) ran the new code.

## Acceptance criteria

- [x] When the shift-start sync moves HEAD and the commits it brought in change `RunDarkFactory.ps1` or a file under `DarkFactory/`, the shift runs the new code before it claims, merges or watches anything: either the coordinator starts itself again with the same arguments (in the same herdr tab or console, never `Start-Process`), or `-Restart` and `-NewTab` fast-forward the checkout before they launch the coordinator. Notes say which and why.
- [x] The log says so in one line naming the old and new commits (for example `sync    factory code changed 8c14126..; restarting on it`).
- [x] A sync that brings in no factory-code change starts the shift as it does today, with no extra line.
- [x] A self-test (`-TestCoordinatorWatchdog` or the area's own switch) covers both cases with a stand-in for the sync, 0 FAIL under Windows PowerShell and `pwsh`.

## Notes

- Approach: the coordinator does it itself (ADR-1170). `Invoke-ShiftStartSync` in `DarkFactory/ShiftCoordinator.ps1` notes HEAD before and after the sync. When the commits brought in change `RunDarkFactory.ps1` or `DarkFactory/`, it traces `sync    factory code changed <old>..<new>; restarting on it` and runs `& $FactoryScript` again in the same process and console, with the same bound parameters plus the shift's `-ShiftStamp`, before anything is claimed, merged or watched. Why: this covers every way a coordinator starts (plain start, `-Restart`, `-NewTab`, the next `-Continuous` shift, a watchdog replacement), not only the two launchers. It also keeps the pid and command line that the stamp, the watchdog and `-Restart` go by, and one shift keeps one trace log. The `git`/`gh` timeout wrappers are removed before the re-run, so the new run defines its own.
- `DarkFactory/AffectedTests.ps1` and `DarkFactory/Restart.ps1` needed no change.
- `-TestCoordinatorWatchdog` runs 5 new checks on a throwaway repository with a stand-in sync: no move, a non-factory commit, a DarkFactory file, RunDarkFactory.ps1, and a refusal. 21 PASS / 0 FAIL under Windows PowerShell and pwsh. dotnet build is clean and the fast tests are green.
- I did not run a real shift restart end to end in this lane (it would start a shift). The re-run is exercised the next time a sync brings in factory code.

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: 7ed38ace c7955563 b38f9a13 fd932272.
- 2026-10-31: Doing -> Done. A coordinator whose shift-start sync brings in factory code re-runs itself on it before claiming anything (ADR-1170)
