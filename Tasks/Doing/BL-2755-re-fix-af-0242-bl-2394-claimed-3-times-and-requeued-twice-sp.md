---
id: BL-2755
title: Re-fix AF-0242: BL-2394 claimed 3 times and requeued twice: split for size, then blocked on a missing engine hook, before finishing
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-18
completed:
---
# BL-2755 — Re-fix AF-0242: BL-2394 claimed 3 times and requeued twice: split for size, then blocked on a missing engine hook, before finishing

## Goal

The defect the audit office reported as AF-0242 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0242 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0242-bl-2394-claimed-3-times-and-requeued-twice-split-f.md`.

Re-fix: the earlier task(s) BL-2579 reached Done, and a later re-audit by the process auditor found the reproduction still reproduces:

- 2026-10-18 | 2026-10-18_1045.md | reproduces: yes | The original reproduction still shows BL-2394 with claims 3 (REQUEUE 'Split: too big for one run', REQUEUE 'Waits on BL-2479: the engine has no hook', DONE 09:38:46). Re-audited by mechanism over 2026-10-16 onward: the same cause, split for size and then blocked on a missing piece of the runner/engine, crossed again with BL-2529. It was claimed 3 times, requeued at 03:35:00 L5 ('Split for size') and at 05:59:24 L1 ('Waits on BL-2601: the case runner skips cases 520 to 537'), and finished at 08:37:34 L1 on 2026-10-17.

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

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
