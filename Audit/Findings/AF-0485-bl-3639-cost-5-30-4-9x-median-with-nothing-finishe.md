---
id: AF-0485
title: BL-3639 cost $5.30 (4.9x median) with nothing finished: a requeue on held files, then 81 turns in 14 min at the cap
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:BL-3639:cost-outlier
reproduction: none
task: BL-4238
tasks: BL-4238
found: 2026-10-29
found-at: ce4fb84772365b99da350fd339cc0c2e9cb2ce30
scorecard: 2026-10-29_0606.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0485 - BL-3639 cost $5.30 (4.9x median) with nothing finished: a requeue on held files, then 81 turns in 14 min at the cap

## Summary

Low finding from the process auditor at `logs/BL-3639-20261028-052757-L9.jsonl`: BL-3639 cost $5.30 (4.9x median) with nothing finished: a requeue on held files, then 81 turns in 14 min at the cap.

## Evidence

Location: `logs/BL-3639-20261028-052757-L9.jsonl`

Run L6 on 2026-10-26: 27 turns, 14.4 min, requeued waiting on files held by BL-3570 and BL-3893 (DarkFactory-20261026-151632-L6.log:104). Run L9 on 2026-10-28, 06:06:02 to 06:20:14: 81 turns in 14.2 min, stopped at the 80-turn cap (DarkFactory-20261028-052757-L9.log:20). The task was then Deferred and split. All $5.30 was spent with no task Done.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).tasks | ? id -eq 'BL-3639'
```

- Expected: No task over 3x the median costUsd and over 5 USD.
- Actual: BL-3639 costUsd 5.304, claims 2, outcome requeued.

## Re-audits

## Log

- 2026-10-29: filed proposed.
- 2026-10-29: proposed -> accepted.
