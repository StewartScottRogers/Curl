---
id: AF-0465
title: BL-3563 claimed 3 times and requeued 3 times before it was split and Deferred
auditor: process
severity: Medium
status: accepted
reason: 
key: process:logs:BL-3563:redone-work
reproduction: none
task: BL-4183
tasks: BL-4183
found: 2026-10-29
found-at: ce4fb84772365b99da350fd339cc0c2e9cb2ce30
scorecard: 2026-10-29_0606.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0465 - BL-3563 claimed 3 times and requeued 3 times before it was split and Deferred

## Summary

Medium finding from the process auditor at `logs/DarkFactory-20261028-052757-L1.log:53`: BL-3563 claimed 3 times and requeued 3 times before it was split and Deferred.

## Evidence

Location: `logs/DarkFactory-20261028-052757-L1.log:53`

Claim 1 (L5, 2026-10-26 09:30:59) ran 17 turns and was requeued at 09:39:14: 'Needs BL-3846 ... and BL-3847 first' (DarkFactory-20261026-063535-L5.log:152). Claim 2 (L5, 15:19:03) ran 62 turns over 33.7 min and was requeued at 15:57:12: 'Needs BL-3890 ... the multi wiring is in the stash' (DarkFactory-20261026-151632-L5.log:33). Claim 3 (L1, 2026-10-28 06:01:02) ran 81 turns over 53.3 min and was requeued at 06:56:09 on the 80-turn cap (DarkFactory-20261028-052757-L1.log:53). The task was then Deferred and split into BL-3976, BL-3977 and BL-3978. The dependencies were found by running the task, not before claiming it, and the split came only after a capped run. Total cost $9.19.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; Select-String -Path Z:/repos/Curl.audit/20261029-060658/logs/DarkFactory-*-L*.log -Pattern 'BL-3563 (claim|end|REQUEUE)'
```

- Expected: A task claimed fewer than 3 times and requeued fewer than twice.
- Actual: BL-3563 claims=3, 3 REQUEUE lines, outcome requeued, then Deferred and split.

## Re-audits

- 2026-10-31 | 2026-10-31_0358.md | reproduces: yes | Rule re-run over this window (Since 2026-10-29): tasksClaimedMoreThanOnce 50, requeues 66. Tasks were again requeued repeatedly before being split and Deferred. BL-4112 was requeued at 10:18:11 and 16:25:53 (80-turn cap), then split by split-BL-4112-20261029-164929-L7 and Deferred. BL-4328 was requeued at 22:16:33 and 00:07:54 (80-turn cap), then split at 13:04:46 and Deferred. BL-4131 was requeued at 18:56:56 and 08:09:38, then split at 15:46:39 and Deferred.

## Log

- 2026-10-29: filed proposed.
- 2026-10-29: proposed -> accepted.
