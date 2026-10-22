---
id: AF-0326
title: BL-2970 claimed 3 times and requeued 3 times: a file outside its touches, a stash that did not apply, then two cost-cap requeues, before it was deferred and split
auditor: process
severity: Medium
status: proposed
reason:
key: process:logs:BL-2970:redone-work
reproduction: none
task: none
tasks:
found: 2026-10-22
found-at: 167e765b2a009a4887fc971075283ef9d24a73a1
scorecard: 2026-10-22_0130.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0326 - BL-2970 claimed 3 times and requeued 3 times: a file outside its touches, a stash that did not apply, then two cost-cap requeues, before it was deferred and split

## Summary

Medium finding from the process auditor at `logs/DarkFactory-20261021-064209-L1.log:356`: BL-2970 claimed 3 times and requeued 3 times: a file outside its touches, a stash that did not apply, then two cost-cap requeues, before it was deferred and split.

## Evidence

Location: `logs/DarkFactory-20261021-064209-L1.log:356`

Claim 1 (L1 of shift 064209): 12:47:17 to 13:02:52, 72 turns, $4.27. 'stash uncommitted work kept'. At 13:03:22 REQUEUE: 'Needs Curl.Engine.UnitTests (EngineCompositionDoh*Tests pin the 8.21.0 DoH lines), which BL-2921 in Doing touches; code is written and stashed'. Claim 2 (DarkFactory-20261021-140711-L8.log) at 14:39:09: 'stash 0725a3f9 from an earlier run did not apply cleanly'. It ran 13 turns ($0.87), then at 14:44:43 REQUEUE 'Cost cap: library side ported onto the BL-2981 split files'. Claim 3 (DarkFactory-20261021-150011-L2.log) at 17:41:45 applied stash 2b0064ac. It ran 5 turns ($0.46), then at 17:43:14 REQUEUE 'Cost cap: this claim cap of $1.02 cannot cover the trace and Engine tests...'. The task Log then records Backlog -> Deferred, split into BL-3011, BL-3012 and BL-3013. BL-3011 was itself PARKED at 20:58:14 ('push kept being refused'). The root cause was a touches list that missed a test file the change had to edit. Then the cost cap cut each later claim short. In total $5.60 was spent and BL-2970 itself finished nothing.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-19 -LogRoot Z:\repos\Curl.audit\20261022-013026\logs -CiRunsJson Z:\repos\Curl.audit\20261022-013026\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2970'; Select-String -Path Z:\repos\Curl.audit\20261022-013026\logs\DarkFactory-*-L*.log -Pattern 'BL-2970 +(claim|stash|REQUEUE|end)'
```

- Expected: BL-2970 claimed fewer than 3 times and requeued fewer than twice.
- Actual: claims=3, costUsd=5.602, outcome=requeued; 3 REQUEUE lines (outside touches, cost cap, cost cap); stash 0725a3f9 did not apply cleanly.

## Re-audits

## Log

- 2026-10-22: filed proposed.
