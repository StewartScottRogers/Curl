---
id: AF-0405
title: BL-3315 requeued twice, each time for missing prerequisite work, then deferred over its cost cap and split
auditor: process
severity: Medium
status: accepted
reason: 
key: process:logs:BL-3315:redone-work
reproduction: none
task: none
tasks:
found: 2026-10-23
found-at: 94da765e1b4eb040511b48ff68f7b42c5226e207
scorecard: 2026-10-23_2322.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0405 - BL-3315 requeued twice, each time for missing prerequisite work, then deferred over its cost cap and split

## Summary

Medium finding from the process auditor at `logs/DarkFactory-20261023-110315-L7.log:14`: BL-3315 requeued twice, each time for missing prerequisite work, then deferred over its cost cap and split.

## Evidence

Location: `logs/DarkFactory-20261023-110315-L7.log:14`

Claim 1, L5 07:12:57 (sonnet, 40 turns, 22 min): REQUEUE 07:35:46 'Split: OS_ERRNO needs Abstractions/Networking/Engine/Http work, filed as BL-3328 and BL-3329'. Claim 2, L7 11:06:57 (opus retry, 45 turns, 18 min): REQUEUE 11:32:02 'Waits on BL-3342 (PoolingConnector drops LastSocketError) and BL-3343'. At 17:14:50 and 17:15:20 L3 and L4 logged it 'unclaimable cost cap 2.35 of 3.07'. L4 split it at 17:16:38 into 'BL-3424, BL-3425'. At 17:16:23 BL-3234's split had already taken those same IDs, and L2 renumbered the duplicates to BL-3428/BL-3429 at 17:20:59. Cause: twice claimed before the code it depended on existed. Measured cost $2.35 (5.3x the median).

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-23 -LogRoot Z:\repos\Curl.audit\20261023-232221\logs -CiRunsJson Z:\repos\Curl.audit\20261023-232221\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261023-232221\logs\DarkFactory-*.log -Pattern 'BL-3315 (claim|REQUEUE|split|unclaimable)|BL-342[45] -> '
```

- Expected: BL-3315 claimed once its prerequisites were done, with no REQUEUE
- Actual: two claims, two REQUEUEs for missing prerequisites, unclaimable over cap, split IDs that collided with BL-3234's and were renumbered

## Re-audits

## Log

- 2026-10-23: filed proposed.
- 2026-10-24: proposed -> accepted.
