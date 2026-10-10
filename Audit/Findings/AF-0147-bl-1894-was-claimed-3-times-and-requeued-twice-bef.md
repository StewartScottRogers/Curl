---
id: AF-0147
title: BL-1894 was claimed 3 times and requeued twice before one run finished it
auditor: process
severity: Low
status: accepted
reason: 
key: process:BL-1894:BL-1894:redone-work
reproduction: none
task: none
tasks:
found: 2026-10-10
found-at: 1b27494521dec4bdaa3fe60c8dc7a3fc73874253
scorecard: 2026-10-10_0123.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0147 - BL-1894 was claimed 3 times and requeued twice before one run finished it

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261009-213949-L1.log:202`: BL-1894 was claimed 3 times and requeued twice before one run finished it.

## Evidence

Location: `logs/DarkFactory-20261009-213949-L1.log:202`

Measure-FactoryProcess.ps1 reports BL-1894 claims=3 (rule: 3 or more). Claims: DarkFactory-20261009-162621-L2.log:212 (19:12:18), DarkFactory-20261009-213949-L1.log:187 (22:47:35), DarkFactory-20261010-004419-L1.log:2 (00:44:34). Requeue 1 at L2.log:218 (19:14:03): 'Split into BL-1930, BL-1931 and BL-1932'. The opus run spent 9 turns and $0.55 only to split the task, which planning could have done. Requeue 2 at 213949-L1.log:202 (22:53:20): 'Waits on BL-1944: test1445 ... needs %PWD/%SRCDIR values'. That run spent 33 turns and $1.75 before finding a missing dependency. The third run (20 turns, $0.85) finished it. Waste: $2.30 and about 7 lane-minutes, under the 20%-of-lane-time Medium bar.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-09 -LogRoot Z:\repos\Curl.audit\20261010-012326\logs -CiRunsJson Z:\repos\Curl.audit\20261010-012326\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261010-012326\logs\DarkFactory-*-L*.log -Pattern 'BL-1894 (claim|REQUEUE)'
```

- Expected: BL-1894 claimed once or twice, never requeued twice
- Actual: tasks[] BL-1894 claims=3; 3 claim lines and 2 REQUEUE lines (19:14:03 split, 22:53:20 waits on BL-1944)

## Re-audits

## Log

- 2026-10-10: filed proposed.
- 2026-10-10: proposed -> accepted.
