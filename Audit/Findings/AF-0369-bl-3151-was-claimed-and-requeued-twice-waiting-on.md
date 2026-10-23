---
id: AF-0369
title: BL-3151 was claimed and requeued twice waiting on a shift-end merge it could not cause; the second claim came from a shift started for it during that merge's CI wait
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:BL-3151:redone-work
reproduction: none
task: BL-3222
tasks: BL-3222
found: 2026-10-22
found-at: 9ada2459e76878e927865fe930a3afac67191f22
scorecard: 2026-10-22_1902.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0369 - BL-3151 was claimed and requeued twice waiting on a shift-end merge it could not cause; the second claim came from a shift started for it during that merge's CI wait

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261022-172108-L1.log`: BL-3151 was claimed and requeued twice waiting on a shift-end merge it could not cause; the second claim came from a shift started for it during that merge's CI wait.

## Evidence

Location: `logs/DarkFactory-20261022-172108-L1.log`

Measure-FactoryProcess: BL-3151 claims=2, outcome=requeued, costUsd=0.9619, minutes=1.35. Task Log: 'Doing -> Backlog. Waiting for the shift-end merge to put BL-3147 (fb690a3b1) on master...' and then 'Still waiting for the shift-end merge... Set lane: no until that holds'. Claim 1: L1 at 17:08:48 (shift 164804), 26 turns, $0.643, REQUEUE 17:11:59. Then DarkFactory-20261022-164804.log at 17:21:07 says 'BL-3151 is claimable during the merge's CI wait; next shift started'. Claim 2: L1 at 17:21:30 (shift 172108), 12 turns, $0.319, REQUEUE 17:22:51. The merge it waited for came at 17:42:15 ('merged 2f68618 into master (pull request #209)'). So the coordinator started a shift to claim a task whose only blocker was the merge it was still waiting on. Nothing to finish at either claim. Waste: 2 Opus runs, $0.96, no task done. Under 20% of lane time, so Low.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261022-190247\logs -CiRunsJson Z:\repos\Curl.audit\20261022-190247\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261022-190247\logs\DarkFactory-20261022-1*.log -Pattern 'BL-3151 (claim|REQUEUE|is claimable)'
```

- Expected: No task requeued twice. A task waiting on the shift-end merge is not claimed again before that merge lands.
- Actual: BL-3151 claims=2, outcome=requeued. 2 claims (17:08:48, 17:21:30), 2 REQUEUE lines, and '17:21:07 BL-3151 is claimable during the merge's CI wait; next shift started' before 'merged 2f68618 into master' at 17:42:15.

## Re-audits

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
