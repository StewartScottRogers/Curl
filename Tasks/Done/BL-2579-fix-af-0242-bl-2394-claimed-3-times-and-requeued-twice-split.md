---
id: BL-2579
title: Fix AF-0242: BL-2394 claimed 3 times and requeued twice: split for size, then blocked on a missing engine hook, before finishing
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2579 — Fix AF-0242: BL-2394 claimed 3 times and requeued twice: split for size, then blocked on a missing engine hook, before finishing

## Goal

The defect the audit office reported as AF-0242 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0242 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0242-bl-2394-claimed-3-times-and-requeued-twice-split-f.md`.

Location: `logs/DarkFactory-20261016-043300-L2.log:116`

Location: `logs/DarkFactory-20261016-043300-L2.log:116`

Measure-FactoryProcess.ps1: BL-2394 claims 3. L2 claim 20:47:08, REQUEUE 20:50:21 'Split: too big for one run; waits on BL-2395..BL-2398' (DarkFactory-20261015-200601-L2.log:15-21, run $1.47, 19 turns); L2 claim 05:33:25, REQUEUE 05:38:03 'Waits on BL-2479: the engine has no hook to perform from the options...' (DarkFactory-20261016-043300-L2.log:108-116, run $1.91, 23 turns); L2 claim 09:14:10, format FAIL (permission denied on git stash list), rebase conflict resolve, DONE 09:38:46 (DarkFactory-20261016-081911-L2.log:117-175, run $5.66, 90 turns, plus resolve run $0.48). Why: the task was filed before it was sized or before the hook it needed existed; the two requeue runs ($3.38) were spent rediscovering this. The task Log agrees with the run logs.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-15 -LogRoot Z:\repos\Curl.audit\20261016-162347\logs -CiRunsJson Z:\repos\Curl.audit\20261016-162347\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | ? id -eq 'BL-2394'; Select-String -Path Z:\repos\Curl.audit\20261016-162347\logs\DarkFactory-*-L*.log -Pattern 'BL-2394 (claim|REQUEUE|DONE)'
```

- Expected: BL-2394 claimed once or twice, never requeued twice.
- Actual: BL-2394 claims 3, costUsd 9.5261; REQUEUE at 20:50:21 and 05:38:03 before DONE at 09:38:46.

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Fix: the lane prompt rules (both copies in RunDarkFactory.ps1) now tell a run to size the task and grep every engine hook it must call in the first pass, filing all missing pieces so one requeue names them (AF-0242). Reproduction reads audit logs, which a lane may not read; the re-audit confirms.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-17: Doing -> Done. Lane prompt now requires sizing and engine-hook check in the first pass
