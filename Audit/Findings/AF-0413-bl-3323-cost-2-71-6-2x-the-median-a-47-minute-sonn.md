---
id: AF-0413
title: BL-3323 cost $2.71 (6.2x the median): a 47-minute Sonnet run requeued at the end because BL-3316 held Finish.cs, then an Opus retry
auditor: process
severity: Low
status: proposed
reason:
key: process:BL-3323:cost-outlier
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
# AF-0413 - BL-3323 cost $2.71 (6.2x the median): a 47-minute Sonnet run requeued at the end because BL-3316 held Finish.cs, then an Opus retry

## Summary

Low finding from the process auditor at `logs/BL-3323-20261023-035126-L7.jsonl`: BL-3323 cost $2.71 (6.2x the median): a 47-minute Sonnet run requeued at the end because BL-3316 held Finish.cs, then an Opus retry.

## Evidence

Location: `logs/BL-3323-20261023-035126-L7.jsonl`

Run 1 (L7, sonnet, 43 turns, 47.1 min, $0.98) passed its tests repeatedly (2405/2405, 2406/2406) and then requeued at 07:25:15: 'Finish.cs is held by BL-3316 (Doing)'. Its work was stashed. Run 2 (L6 11:08:15, opus retry, 34 turns, 17 min, $1.74) applied stash b0637982 and finished at 11:27:47. Cost drivers: a file overlap discovered only at integration, and the retry being raised to Opus for work that was already done.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-23 -LogRoot Z:\repos\Curl.audit\20261023-232221\logs -CiRunsJson Z:\repos\Curl.audit\20261023-232221\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261023-232221\logs\DarkFactory-*-L*.log -Pattern 'BL-3323 (claim|end|stash|REQUEUE|DONE)'
```

- Expected: no task over 3 times the median costUsd
- Actual: BL-3323 costUsd 2.7136, claims 2; REQUEUE 'Finish.cs is held by BL-3316', then an opus retry

## Re-audits

## Log

- 2026-10-23: filed proposed.
