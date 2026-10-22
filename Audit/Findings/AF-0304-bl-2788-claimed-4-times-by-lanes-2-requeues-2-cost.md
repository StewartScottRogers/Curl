---
id: AF-0304
title: BL-2788 claimed 4 times by lanes (2 requeues, 2 cost-cap blocks); its first run's 19 minutes of work sat in a stash that did not apply cleanly
auditor: process
severity: Medium
status: accepted
reason: 
key: process:logs:BL-2788:redone-work
reproduction: none
task: BL-2942
tasks: BL-2942
found: 2026-10-19
found-at: 1b3c70b1436b2d499099c5c6dd7bb119e11f5631
scorecard: 2026-10-19_2134.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0304 - BL-2788 claimed 4 times by lanes (2 requeues, 2 cost-cap blocks); its first run's 19 minutes of work sat in a stash that did not apply cleanly

## Summary

Medium finding from the process auditor at `logs/DarkFactory-20261019-061607-L1.log:62`: BL-2788 claimed 4 times by lanes (2 requeues, 2 cost-cap blocks); its first run's 19 minutes of work sat in a stash that did not apply cleanly.

## Evidence

Location: `logs/DarkFactory-20261019-061607-L1.log:62`

Tool row: BL-2788 claims 4, minutes 0.27, costUsd 4.6076, outcome blocked. (1) DarkFactory-20261018-192454-L3.log:28-65: claimed at 22:42:22. 79 turns and 19 min ($4.07) of edits to the RTSP handler, reader and server, then 'test 15/16 pass'. Requeued at 23:01:44 with 'Waits on BL-2789 (RTSP DESCRIBE body to the write callback)'. The work went to stash c61946ed. This dependency was found mid-run, not at filing. (2) DarkFactory-20261019-061607-L2.log:30-33, 06:58:38: claimed only for the run to be refused: 'its runs of the last day already cost 4.07 of its 4.61 US dollar cost cap', then BLOCKED. (3) DarkFactory-20261019-061607-L1.log:60-66: the cost-cap window passed, claimed at 07:02:18. 'stash c61946ed from an earlier run did not apply cleanly; the run is told its hash'. 8 turns ($0.53), then requeued 'Split for the cost cap' into BL-2818 and BL-2819. (4) DarkFactory-20261019-092012-L2.log:2-5, 09:21:24: claimed again and refused at once: '4.61 of its 3.31 US dollar cost cap'. The cap fell from 4.61 to 3.31 between claims. An interactive session finished it outside the cap. c61946ed is no longer in git stash list. Its work was carried into BL-2818 and BL-2819 per the task Log, so it is not recorded as lost. Waste: two claims that could only block, and a resumed run that could not reuse its stash.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-18 -LogRoot Z:\repos\Curl.audit\20261019-213437\logs -CiRunsJson Z:\repos\Curl.audit\20261019-213437\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2788'; Select-String -Path Z:\repos\Curl.audit\20261019-213437\logs\DarkFactory-*-L*.log -Pattern 'BL-2788 (claim|stash|cost cap|REQUEUE|BLOCKED)'
```

- Expected: BL-2788 claimed fewer than 3 times; no claim of a task already over its cost cap.
- Actual: claims 4; 2 REQUEUE, 2 BLOCKED by the cost cap, 1 stash that did not apply cleanly.

## Re-audits

- 2026-10-22 | 2026-10-22_0130.md | reproduces: yes | The original reproduction still shows BL-2788 claims=4, outcome blocked, with claim, stash, cost-cap and REQUEUE lines across 4 lane logs. The same mechanism crosses the redone-work rule in this window. BL-2970 was claimed 3 times and requeued 3 times: it needed a file outside its touches, stash 0725a3f9 'did not apply cleanly' (140711-L8 14:39:13), then came two cost-cap requeues and a split. BL-2925 was claimed 3 times: outside touches, then 'Run cost budget used up', then BLOCKED by the cost cap at 17:52:01 (175054-L2).

## Log

- 2026-10-19: filed proposed.
- 2026-10-21: proposed -> accepted.
