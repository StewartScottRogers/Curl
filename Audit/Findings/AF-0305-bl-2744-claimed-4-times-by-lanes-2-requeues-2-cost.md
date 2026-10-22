---
id: AF-0305
title: BL-2744 claimed 4 times by lanes (2 requeues, 2 cost-cap blocks): a file outside its touches, a stash that did not apply, then claims refused by the cost cap
auditor: process
severity: Medium
status: proposed
reason:
key: process:logs:BL-2744:redone-work
reproduction: none
task: none
tasks:
found: 2026-10-19
found-at: 1b3c70b1436b2d499099c5c6dd7bb119e11f5631
scorecard: 2026-10-19_2134.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0305 - BL-2744 claimed 4 times by lanes (2 requeues, 2 cost-cap blocks): a file outside its touches, a stash that did not apply, then claims refused by the cost cap

## Summary

Medium finding from the process auditor at `logs/DarkFactory-20261019-061607-L1.log:86`: BL-2744 claimed 4 times by lanes (2 requeues, 2 cost-cap blocks): a file outside its touches, a stash that did not apply, then claims refused by the cost cap.

## Evidence

Location: `logs/DarkFactory-20261019-061607-L1.log:86`

Tool row: BL-2744 claims 4, costUsd 2.378, outcome blocked. (1) DarkFactory-20261018-192454-L3.log:88-109: claimed at 23:20:34, 40 turns and 10.4 min of HttpProtocolHandler edits with tests passing. Requeued at 23:31:20 with 'Needs Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, which BL-2694 (in Doing) touches'. The task's touches at claim did not cover the file it needed. Work went to stash 7eef9059. (2) DarkFactory-20261019-061607-L1.log:85-92, 07:24:34: claimed; 'stash 7eef9059 from an earlier run did not apply cleanly'. Requeued at 07:27:24 with 'Waits on BL-2820'. (3) DarkFactory-20261019-092012-L2.log:6-9 (09:21:53) and DarkFactory-20261019-112913-L1.log:24-27 (11:42:24): claimed twice and refused at once both times ('already cost 3.16 of its 3.31 US dollar cost cap'), then BLOCKED. The second refusal came after the first had already shown the cap was spent. The task Log adds two interactive claims, so 6 in all, before Done.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-18 -LogRoot Z:\repos\Curl.audit\20261019-213437\logs -CiRunsJson Z:\repos\Curl.audit\20261019-213437\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2744'; Select-String -Path Z:\repos\Curl.audit\20261019-213437\logs\DarkFactory-*-L*.log -Pattern 'BL-2744 (claim|stash|cost cap|REQUEUE|BLOCKED)'
```

- Expected: BL-2744 claimed fewer than 3 times.
- Actual: claims 4; 2 REQUEUE, 2 BLOCKED by the cost cap (09:22:08, 11:42:39), 1 stash that did not apply cleanly.

## Re-audits

- 2026-10-22 | 2026-10-22_0130.md | reproduces: yes | The original reproduction still shows BL-2744 claims=4, outcome blocked. The same cause recurs in this window: a file outside touches, a stash that did not apply, then cost-cap refusals. BL-2970 (064209-L1 13:03:22 REQUEUE 'Needs Curl.Engine.UnitTests... which BL-2921 in Doing touches'; 140711-L8 14:39:13 stash did not apply cleanly; cost-cap REQUEUEs at 14:44:43 and 17:43:14). BL-2925 (outside touches at 10:17:52, cost budget at 15:37:20, cost-cap BLOCKED at 17:52:01).

## Log

- 2026-10-19: filed proposed.
