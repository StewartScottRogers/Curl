---
id: BL-5592
title: Fix AF-0904: CI red spell from run 44566263056 (Quality gates, Windows) was first traced 3h27m after the run ended; no coordinator ran in between
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-11-19
completed:
---
# BL-5592 — Fix AF-0904: CI red spell from run 44566263056 (Quality gates, Windows) was first traced 3h27m after the run ended; no coordinator ran in between

## Goal

The cause of the factory behaviour the audit office reported as AF-0904 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0904 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0904-ci-red-spell-from-run-44566263056-quality-gates-wi.md`.

Location: `logs/DarkFactory-20261119-134021.log:13`

Location: `logs/DarkFactory-20261119-134021.log:13`

ciRedSpells[0]: start 2026-11-19T17:19:15Z, end 21:11:44Z, 232.48 min, runId 44566263056. Run 44566263056 ended 17:19:15Z (10:19 local); runs 44585647013 (ended 18:42Z) and 44586512674 (ended 19:10Z) also failed inside the spell. The first coordinator ci trace naming any of them is 13:46:12 (DarkFactory-20261119-134021.log), 3h27m, 2h04m and 1h36m after their ends. The previous coordinator log (DarkFactory-20261119-082213.log) ends at 10:19:25 on handover, and the next coordinator started 13:40:24, so nobody watched CI for about 3h20m. The 30-minute rule is crossed. The 14:12:12 trace is the pass-since line. The runner-capacity run 44557476016 is infrastructure and excluded (ADR-1235).

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-19 -LogRoot <log folder> -CiRunsJson <log folder>\ci-runs.json -OutFile process.json; Select-String -Path <log folder>\DarkFactory-20261119-*.log -Pattern '44566263056'
```

- Expected: A ci trace line naming the run (filed, covered or passed since) within 30 minutes of the run ending.
- Actual: First trace 13:46:12 local, run ended 10:19 local; ciRedSpells[0] is 232.48 minutes.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause (found 2026-11-20): the 2026-11-19 09:40 hand-over shift (DarkFactory-20261119-094033.log) found the main checkout dirty with an interactive session's new task files, which were not committed until 11:14. `Wait-ForCleanTree` waited its 30-minute window, the shift refused at 10:10 with the alarm and exited, and no coordinator ran (so nobody watched CI) until a session restarted the factory at 13:40. The 05:10 shift the same day refused at once because the checkout was one unpushed commit ahead of origin.
- Fix: `DarkFactory/Park.ps1` gains `Wait-ForHandOverReady`, the wait both start checks share. A -Continuous hand-over polls every 2 minutes, runs the coordinator's CI watch on every poll (`Invoke-CiWatch`, which files from its own ci-watch worktree), labels its tab "WAITING, read" once after 30 minutes, and refuses with the alarm only after its own -Hours. `Wait-ForCleanTree` uses it; the new `Wait-ForSyncedCheckout` waits the same way on a checkout ahead of or diverged from origin. `DarkFactory/ShiftCoordinator.ps1` passes the CI watch to both. A start that is not a hand-over still refuses at once. The script help in `RunDarkFactory.ps1` says so.
- Tests: `-TestPark` (28 cases, 8 new or changed: the 94-minute 2026-11-19 case, the -Hours give-up, the ahead-of-origin wait, no wait without a hand-over), `-TestCoordinatorWatchdog` (29), `-TestShiftBranch` (3), `-TestCiWatch` (99), each under Windows PowerShell and PowerShell 7: all pass. `dotnet build`: 0 errors; fast tests: 115 test projects passed, exit 0.
- Criterion 1 needs a shift run on the new code: the task goes back to Backlog (lane: no) until one has, then an interactive session runs the measurement.

## Log

- 2026-11-19: Created.
- 2026-11-20: Backlog -> Doing. Criteria at claim: 87cd20bd 9742157d.
- 2026-11-20: Doing -> Backlog. Fix committed; criterion 1 needs a factory shift to run on the new code before the measurement, interactive only (lane: no)
