---
id: AF-0263
title: BL-2529 claimed 3 times and requeued twice: split for size, then held by a case-runner gap (BL-2601), before finishing
auditor: process
severity: Medium
status: accepted
reason: 
key: process:logs:BL-2529:redone-work
reproduction: none
task: BL-2774
tasks: BL-2774
found: 2026-10-18
found-at: 19ea231be8b4d749462d15058a3c6b97c7e70836
scorecard: 2026-10-18_1045.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0263 - BL-2529 claimed 3 times and requeued twice: split for size, then held by a case-runner gap (BL-2601), before finishing

## Summary

Medium finding from the process auditor at `logs/DarkFactory-20261017-054934-L1.log:26`: BL-2529 claimed 3 times and requeued twice: split for size, then held by a case-runner gap (BL-2601), before finishing.

## Evidence

Location: `logs/DarkFactory-20261017-054934-L1.log:26`

Measure-FactoryProcess.ps1: BL-2529 claims 3, 9.9 min, $4.20, done. Claim 1 at 03:23:39 L5 (DarkFactory-20261017-021554-L5.log:97), requeued at 03:35:00: 'Split for size: waits on BL-2595 (lib530) and BL-2596 (lib525, lib526, lib533)' (24 turns, $1.31). Claim 2 at 05:49:52 L1 (DarkFactory-20261017-054934-L1.log:2), requeued at 05:59:24: 'Waits on BL-2601: the case runner skips cases 520 to 537 as not ported although the six ports are registered'. Its work went to stash 4ce432d4 (46 turns, $1.93). Claim 3 at 08:27:40 L1 applied stash 4ce432d4 and finished at 08:37:34 (25 turns, $0.96). Two runs ($3.24) ended in requeues because the task was too big and depended on runner work that was not filed when it was claimed. The task's Log agrees with the run logs.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-16 -LogRoot Z:\repos\Curl.audit\20261018-104525\logs -CiRunsJson Z:\repos\Curl.audit\20261018-104525\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | ? id -eq 'BL-2529'; Select-String -Path Z:\repos\Curl.audit\20261018-104525\logs\DarkFactory-*-L*.log -Pattern 'BL-2529 (claim|REQUEUE|DONE)'
```

- Expected: BL-2529 claimed once and Done without a requeue.
- Actual: claims 3, costUsd 4.2003; REQUEUE 03:35:00 (split for size), REQUEUE 05:59:24 (waits on BL-2601), DONE 08:37:34.

## Re-audits

## Log

- 2026-10-18: filed proposed.
- 2026-10-18: proposed -> accepted.
