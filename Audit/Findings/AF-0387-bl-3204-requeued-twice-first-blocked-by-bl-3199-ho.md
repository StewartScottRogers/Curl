---
id: AF-0387
title: BL-3204 requeued twice: first blocked by BL-3199 holding ManagedEasyTransfer.cs, then out of its cost cap with 7 Engine tests failing
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:BL-3204:redone-work
reproduction: none
task: BL-3364
tasks: BL-3364
found: 2026-10-23
found-at: 32b1dda228a5fded8978ed4ee514a932b0728722
scorecard: 2026-10-23_0618.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0387 - BL-3204 requeued twice: first blocked by BL-3199 holding ManagedEasyTransfer.cs, then out of its cost cap with 7 Engine tests failing

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261023-005934-L2.log:15`: BL-3204 requeued twice: first blocked by BL-3199 holding ManagedEasyTransfer.cs, then out of its cost cap with 7 Engine tests failing.

## Evidence

Location: `logs/DarkFactory-20261023-005934-L2.log:15`

First claim, L7 at 22:48:46: requeued at 22:52:55 after 19 Opus turns ($0.86) and no code, because 'The fix needs Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, and BL-3199, which is in Doing, holds that file' (BL-3204-20261022-192710-L7.jsonl). Second claim, L2 at 01:00:14 as an Opus retry: requeued at 01:07:44 after 38 turns ($1.75), 'Out of this run's cost cap with 7 Engine unit tests still failing'. Its uncommitted work was stashed (L2.log:13). At 03:51:46 L1 found it unclaimable over its cost cap ($2.60 of $3.72) and split it into BL-3271..BL-3273. BL-3271 (L2, 03:54:18 to 04:21:49, $2.91) landed the stashed work. Its first test run, at 03:55:48, shows the same 7 failures (2376/2383), so the stash was recovered and no work was lost. Total: $5.5 and 4 runs for one change.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261023-061815\logs -CiRunsJson Z:\repos\Curl.audit\20261023-061815\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261023-061815\logs\DarkFactory-*.log -Pattern 'BL-3204 (claim|REQUEUE|stash|split|unclaimable)'
```

- Expected: At most one requeue for BL-3204.
- Actual: Two REQUEUE lines (22:52:55 L7 and 01:07:44 L2), then a cost-cap split at 03:53:43.

## Re-audits

- 2026-10-23 | 2026-10-23_2322.md | reproduces: yes | Same cause crossed the rule again: BL-3412 was requeued twice (L4 14:55:22 'Depends on BL-3410', L3 17:26:39 cost cap reached with work stashed), then was unclaimable over its cap and split at 19:14:54. BL-3315 was requeued twice (L5 07:35:46, L7 11:32:02 'Waits on BL-3342'), then split over its cap at 17:16:38. BL-3234 was requeued twice (L3 04:46:47, L1 11:08:44 waiting on BL-3334), then split over its cap at 17:16:23.

## Log

- 2026-10-23: filed proposed.
- 2026-10-23: proposed -> accepted.
