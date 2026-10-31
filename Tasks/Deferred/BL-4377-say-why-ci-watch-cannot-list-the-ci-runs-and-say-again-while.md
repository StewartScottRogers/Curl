---
id: BL-4377
title: Say why CI watch cannot list the CI runs, and say again while it still cannot
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [DarkFactory/CiWatch.ps1, DarkFactory/ShiftEnd.ps1, DarkFactory/Heartbeat.ps1, DarkFactory/ShiftCoordinator.ps1]
requirement: none
created: 2026-10-30
completed:
---
# BL-4377 — Say why CI watch cannot list the CI runs, and say again while it still cannot

## Goal

When the coordinator's CI watch cannot list the `CI` runs, its log line carries gh's own error text, and a watch that stays unable to list says so again (with how long), so a blind watch is told apart from a one-off failure at shift start.

## Context

`Get-CiRuns` (`DarkFactory/CiWatch.ps1`, near line 400) runs `gh run list --workflow CI --branch <b> ...` with `2>$null` and, on a non-zero exit, writes `cannot list the CI runs on <branch> with gh` through `Write-CiRunLine -Key 'list'`, which traces each key once per shift. Every shift since 15:22 on 2026-10-30 (DarkFactory-20261030-152226, -193026, -193800, -205631, -205849) logged that line once, on its first heartbeat, and never again; the 06:06 shift logged 68 other `ci` lines. The same command run by hand in Windows PowerShell at 21:25 exited 0. So either the first heartbeat's call fails for a reason no one can see (gh's stderr is discarded), or the watch stays blind for the whole shift and the once-per-key trace hides it. A CI failure on dbc468ecd (run 41151657472, test670 on Windows) was not filed by the shift that was running (BL-4378 files it by hand).

## Acceptance criteria

- [x] The `cannot list` line includes gh's exit code and the first line of its stderr.
- [x] While listing keeps failing, the line is written again at most every 30 minutes with how long the watch has been unable to list; the first successful list after a failure writes one `listing CI runs again` line.
- [x] `RunDarkFactory.ps1`'s CI-watch self-test (`-TestCiWatch` or the area's own switch) covers the three cases with stand-in `-ListRuns` blocks.
- [ ] The cause of the first-heartbeat failure is named in Notes from a shift's log after this change, and fixed if it is the factory's own (for example a call made before the shift's environment is ready).

## Notes

- 2026-10-31 (interactive): the shift-end merge has the same blindness. DarkFactory-20261030-220408.log: `merge not merged: CI did not finish on 8029938 within 60 min` at 23:33 Arizona, yet CI run on 802993809 was created 05:31 UTC (22:31) and completed success 06:00 UTC (23:00). Find the merge wait's gh call (ShiftEnd.ps1) and give it the same error text and re-trace; a blind wait costs a whole shift's merge to master. Add `DarkFactory/ShiftEnd.ps1` to touches if it changes.
- 2026-10-31 (lane 2): criteria 1-3 done and run. `Invoke-GhRunList` (CiWatch.ps1) keeps gh's exit code and first stderr line; `Write-GhListLine` traces the first failure, again at most every 30 min with how long, and one `listing CI runs again` line on recovery. `-TestCiWatch` covers all three cases with stand-in `-ListRuns` blocks (all PASS). The merge wait (ShiftEnd.ps1 `Wait-MergeCiRun`) uses the same helper and adds gh's failure to its `not merged: CI did not finish` line (`-TestHeartbeat`, 0 FAIL).
- 2026-10-31 (lane 2): likely cause, found by reproduction and NOT yet confirmed from a shift log: BL-4353's timed `gh` wrapper (Heartbeat.ps1 `Invoke-TimedExternalCall`, enabled at coordinator start) cast `$args` to `[string[]]`, so an unquoted `--json databaseId,status,...` (an array to a PowerShell function) became `"databaseId status ..."`, and gh exits 1 with `Unknown JSON field`. That fits every observation: it began with BL-4353 (09:06 on 2026-10-30), before the 15:22 shift; it fails on every call, not only the first heartbeat (the once-per-key trace hid that); and the merge wait's `--json status,conclusion` was blind for the whole 60 min. Reproduced: `[string[]]` joins `databaseId,status,conclusion` with spaces, and `gh run list --json "databaseId status"` prints `Unknown JSON field`. Fixed: `ConvertTo-NativeCallArguments` joins an array argument with commas, as PowerShell does for a native command (test in `-TestCoordinatorWatchdog`, 0 FAIL). Checked end to end with the real wrapper: after `Enable-ExternalCallTimeouts`, both `Invoke-GhRunList` and an unquoted `gh run list ... --json databaseId,status` list the runs, exit 0.
- 2026-10-31 (lane 2): touches widened to DarkFactory/Heartbeat.ps1 (the wrapper's fix) and DarkFactory/ShiftCoordinator.ps1 (its test); no task in Doing on origin/work/dark-factory names either.
- 2026-10-31 (lane 2): criterion 4 stays unticked. It asks for the cause to be named from a shift's log taken after this change, and no shift can run inside a lane. To finish: once these commits are on the shift branch, read the first shift log after them. If it has no `cannot list the CI runs` line (or only one followed by `listing CI runs again`), record that it confirms the cause above and tick the box. If the line is still there, its exit code and gh's stderr now name the real cause: fix it.
- 2026-10-31 (lane 2): dotnet build clean (0 warnings), fast tests 0 failed. The code is left uncommitted for the shift to stash (lane rule 6), but criterion 4 needs that code to run in a shift first, so this task cannot reach Done while its code stays in a stash. Whoever claims it next should commit the stashed change (criteria 1-3 are met and tested) so a shift runs it, then come back for criterion 4; or Stewart restates criterion 4 as "named from reproduction".

## Log

- 2026-10-30: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: 842ca840 c081c1f2 9d88f1e3 cd4e4eec.
- 2026-10-31: Doing -> Backlog. Criteria 1-3 done and tested; likely cause (timed gh wrapper space-joins --json a,b) fixed by reproduction. Criterion 4 needs a shift log after the change, which needs the code committed first: see Notes
- 2026-10-31: Backlog -> Deferred. Split into BL-4384 (commit the stashed work, criteria 1-3) and BL-4385 (name the cause from a shift log, criterion 4)
