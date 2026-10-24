---
id: AF-0415
title: BL-3310 cost $2.68 (6.1x the median) in one 60-turn, 41-minute Opus run porting easytest.py cases
auditor: process
severity: Low
status: accepted
reason: 
key: process:BL-3310:cost-outlier
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
# AF-0415 - BL-3310 cost $2.68 (6.1x the median) in one 60-turn, 41-minute Opus run porting easytest.py cases

## Summary

Low finding from the process auditor at `logs/BL-3310-20261023-035126-L4.jsonl`: BL-3310 cost $2.68 (6.1x the median) in one 60-turn, 41-minute Opus run porting easytest.py cases.

## Evidence

Location: `logs/BL-3310-20261023-035126-L4.jsonl`

One run: opus feature pipeline, claimed 05:51:59, ended 06:33:11, 60 turns, 41.2 min, $2.68, 4.54M cache-read tokens, 44.8k output tokens, Done 06:34:26. Cost driver: a long single Opus session whose context grew over 60 turns. It ended by filing two more tasks (renum BL-3312 -> BL-3323, BL-3313 -> BL-3324 at 06:33:46).

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-23 -LogRoot Z:\repos\Curl.audit\20261023-232221\logs -CiRunsJson Z:\repos\Curl.audit\20261023-232221\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261023-232221\logs\DarkFactory-20261023-035126-L4.log -Pattern 'BL-3310 (claim|end|DONE|renum)'
```

- Expected: no task over 3 times the median costUsd
- Actual: BL-3310 costUsd 2.6789; '60 turns, 41.2 min [model opus: feature pipeline]'

## Re-audits

## Log

- 2026-10-23: filed proposed.
- 2026-10-24: proposed -> accepted.
