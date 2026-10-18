---
id: AF-0242
title: BL-2394 claimed 3 times and requeued twice: split for size, then blocked on a missing engine hook, before finishing
auditor: process
severity: Low
status: proposed
reason:
key: process:BL-2394:BL-2394:redone-work
reproduction: none
task: none
tasks:
found: 2026-10-16
found-at: 16500922292d0d0bcaede88acdabc2500cf08ebf
scorecard: 2026-10-16_1623.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0242 - BL-2394 claimed 3 times and requeued twice: split for size, then blocked on a missing engine hook, before finishing

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261016-043300-L2.log:116`: BL-2394 claimed 3 times and requeued twice: split for size, then blocked on a missing engine hook, before finishing.

## Evidence

Location: `logs/DarkFactory-20261016-043300-L2.log:116`

Measure-FactoryProcess.ps1: BL-2394 claims 3. L2 claim 20:47:08, REQUEUE 20:50:21 'Split: too big for one run; waits on BL-2395..BL-2398' (DarkFactory-20261015-200601-L2.log:15-21, run $1.47, 19 turns); L2 claim 05:33:25, REQUEUE 05:38:03 'Waits on BL-2479: the engine has no hook to perform from the options...' (DarkFactory-20261016-043300-L2.log:108-116, run $1.91, 23 turns); L2 claim 09:14:10, format FAIL (permission denied on git stash list), rebase conflict resolve, DONE 09:38:46 (DarkFactory-20261016-081911-L2.log:117-175, run $5.66, 90 turns, plus resolve run $0.48). Why: the task was filed before it was sized or before the hook it needed existed; the two requeue runs ($3.38) were spent rediscovering this. The task Log agrees with the run logs.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-15 -LogRoot Z:\repos\Curl.audit\20261016-162347\logs -CiRunsJson Z:\repos\Curl.audit\20261016-162347\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | ? id -eq 'BL-2394'; Select-String -Path Z:\repos\Curl.audit\20261016-162347\logs\DarkFactory-*-L*.log -Pattern 'BL-2394 (claim|REQUEUE|DONE)'
```

- Expected: BL-2394 claimed once or twice, never requeued twice.
- Actual: BL-2394 claims 3, costUsd 9.5261; REQUEUE at 20:50:21 and 05:38:03 before DONE at 09:38:46.

## Re-audits

- 2026-10-18 | 2026-10-18_1045.md | reproduces: yes | The original reproduction still shows BL-2394 with claims 3 (REQUEUE 'Split: too big for one run', REQUEUE 'Waits on BL-2479: the engine has no hook', DONE 09:38:46). Re-audited by mechanism over 2026-10-16 onward: the same cause, split for size and then blocked on a missing piece of the runner/engine, crossed again with BL-2529. It was claimed 3 times, requeued at 03:35:00 L5 ('Split for size') and at 05:59:24 L1 ('Waits on BL-2601: the case runner skips cases 520 to 537'), and finished at 08:37:34 L1 on 2026-10-17.

## Log

- 2026-10-16: filed proposed.
