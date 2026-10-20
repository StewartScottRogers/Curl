---
id: BL-2056
title: Fix AF-0163: CI red 32 min after test475 failed on Linux and macOS from BL-1907's commit 7b6794f4 (run 38047662088)
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-11
completed: 2026-10-19
---
# BL-2056 — Fix AF-0163: CI red 32 min after test475 failed on Linux and macOS from BL-1907's commit 7b6794f4 (run 38047662088)

## Goal

The defect the audit office reported as AF-0163 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0163 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0163-ci-red-32-min-after-test475-failed-on-linux-and-ma.md`.

Location: `logs/DarkFactory-20261010-022215.log:31`

Location: `logs/DarkFactory-20261010-022215.log:31`

ciRedSpells[1]: start 2026-10-10T11:19:01Z (04:19 local), end 11:51:01Z, 32 min, runId 38047662088, commit 7b6794f42 'feat(conformance): serve active-mode FTP and uploads in the stand-in' (BL-1907, lane L1). Coordinator at 04:21:53: 'filed: regression test475 on Linux+macOS, run 38047662088' as BL-1957, 3 min after the red. Runs 38048526000 and 38049074289 stayed red. The shift had one lane, which was busy with BL-1908 and BL-1913, so the fix was claimed only at 04:37:45 (DarkFactory-20261010-022215-L1.log:135), 16 min after filing. The fix b1124f69 landed at 04:43:49 and CI was green at 04:51:01. Lanes test only on Windows, so this failure could not be caught before the push. Also, the filed ID BL-1957 collided with a lane-made task ('renum BL-1957 -> BL-1958' at 04:27:29).

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-10 -LogRoot Z:\repos\Curl.audit\20261011-005116\logs -CiRunsJson Z:\repos\Curl.audit\20261011-005116\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 38047662088; Select-String -Path Z:\repos\Curl.audit\20261011-005116\logs\DarkFactory-20261010-022215.log -Pattern 'run 38047662088|filed: regression'
```

- Expected: No spell over 30 minutes.
- Actual: minutes 32 runId 38047662088; '04:21:53 BL-1957 ci filed: regression test475 on Linux+macOS, run 38047662088'

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] This task is obsolete, and closes without a change: AF-0163 is closed.

## Notes

- 2026-10-11 (lane 7): Read `Invoke-CiWatch`, `New-CiFailureTasks`, `Get-CiVerdicts` and `Invoke-Claim` in `RunDarkFactory.ps1`. The 32-minute spell splits into: red to filed 3 min (a two-platform failure is filed at once, no `CiConfirmMinutes` wait), filed to claimed 16 min, claimed to fix pushed 6 min, push to green CI 8 min. Filing is already prompt and High, and `Invoke-Claim` already takes the highest priority ready task via `task-board.ps1 next`. The 16 minutes were the shift's only lane finishing the task it held; no script defect shows in that. Dropping a running task for a CI fix would be a new preemption feature, and `task-board.ps1`, which owns claim order, is a guard file (interactive only).
- The ID collision (BL-1957) is already handled: `Invoke-RenumberDuplicates` / BL-2014 renumber the later task, and the log shows exactly that at 04:27:29.
- The reproduction reads a past audit's logs, so no code change can make it give the expected result; only a later audit can.
- No code changed. Needs Stewart to reject AF-0163 as a single-lane wait, or to ask for lane preemption as a feature.

- 2026-10-19: Stewart had it closed as obsolete. Its criteria were replaced by the one above while it was in the Backlog, and the evidence was checked mechanically first.

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
- 2026-10-11: Doing -> Blocked. Stewart: reject AF-0163 (the 16 min wait was the shift's only lane finishing its task, the script already files in 3 min and claims by priority), or ask for lane preemption as a feature?
- 2026-10-11: Blocked -> Doing.
- 2026-10-11: Doing -> Deferred. No work: Stewart closed its finding on 2026-10-11 (closed-how: stewart) as not a factory defect - the red spell was prompt filing and claiming.
- 2026-10-19: Deferred -> Backlog. Stewart: close it as obsolete (AF-0163 is closed).
- 2026-10-19: Backlog -> Doing. Interactive session: closing it as obsolete. Criteria at claim: 15e3c3ca.
- 2026-10-19: Doing -> Done. Obsolete: AF-0163 is closed.
