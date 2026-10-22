---
id: BL-1772
title: Fix AF-0094: BL-1682 claimed 3 times; lane 9 measured without fixing and lane 2 found it waiting on BL-1715, which its depends-on did not name
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1772 — Fix AF-0094: BL-1682 claimed 3 times; lane 9 measured without fixing and lane 2 found it waiting on BL-1715, which its depends-on did not name

## Goal

The defect the audit office reported as AF-0094 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0094 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0094-bl-1682-claimed-3-times-lane-9-measured-without-fi.md`.

Location: `logs/DarkFactory-20261007-201432-L2.log`

Location: `logs/DarkFactory-20261007-201432-L2.log`

L9 claimed it at 20:51:04, ran 26.7 min and 48 turns ($1.79), and requeued at 21:20:34: 'Measured, not fixed ... more than this run's budget'. L2 claimed it at 21:33:51, ran 1.5 min ($0.73), and requeued at 21:38:14: 'Waits on BL-1715 (lazy per-scheme protocol handlers)'. A claim the board should not have made. L8 claimed it at 21:54:06 and was Done at 22:05:18 by another route (native curl.exe delay-loads DLLs). Total $3.76.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1682' | Select-Object id,claims,outcome,costUsd; Select-String -Path ..\logs\DarkFactory-20261007-201432-L*.log -Pattern 'BL-1682 (claim|REQUEUE|DONE)'
```

- Expected: BL-1682 claimed once.
- Actual: BL-1682 claims 3, costUsd 3.7582; REQUEUE on L9 21:20:34 and L2 21:38:14; DONE on L8 22:05:18

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Cause, traced from the shift 20261007-201432 logs and the board history of BL-1682:

- Claim 1 (L9, 20:51) measured and returned the task to Backlog with only a description of what was left ("more than this run's budget"). It filed no task for the remainder, so the board offered BL-1682 again unchanged.
- Claim 2 (L2, 21:33) did what claim 1 should have done: it filed BL-1715 for the lazy protocol handlers and added it to `depends-on`. It was a second claim in the same shift because the AF-0072 guard (BL-1691, `requeued.txt`: no lane reclaims a task requeued earlier in the same shift) was committed at 21:03, after this shift's lanes had started at 20:14 with the older script. Lanes run the coordinator's copy of the script, so a fix takes effect from the next shift.
- Claim 3 (L8, 21:54) was legitimate as far as the board went: BL-1715 was Done at 21:50.

Fix: the AF-0072 guard already stops claims 2 and 3 in any shift started after 2026-10-07 21:03. This task closes the cause of claim 1: rule 8 of the lane prompt and rule 9 of the coordinator-run prompt in `RunDarkFactory.ps1` now say that a run which cannot finish files what is left as a task with the board script, adds it to `depends-on`, records its measurements under Notes, and moves the task to Backlog with a -Reason naming that task. The next lane then starts on the remaining work instead of measuring the task again.

Reproduction: run with `-Since 2026-10-07` it reads that day's logs, which cannot change, so it will always count 3 claims for BL-1682. The expected result ("claimed once") applies to shifts started after these fixes, and the process auditor's re-audit checks that. A lane cannot run it anyway: `Audit/` is guarded against factory processes. The first box is ticked on the trace of each claim to a closed cause, not on a rerun.

Checks: the script parses (0 parser errors); `dotnet build` clean; fast tests green (0 failed projects).

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Lane prompts now say a run that cannot finish files the remaining work as a depends-on task, so the next claim starts on it (AF-0094)
