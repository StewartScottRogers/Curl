---
id: AF-0482
title: BL-3563 cost $9.19 (8.6x the $1.07 median) over three opus runs, the last stopped at the 80-turn cap
auditor: process
severity: Low
status: closed
reason: Audit 2026-10-31_0358.md: a cost-outlier finding records a past run's cost, which no fix can change; its tasks (BL-4235) are Done at bfe08061f844, so it closes without a re-audit (ADR-0422, amended 2026-10-14).
key: process:logs:BL-3563:cost-outlier
reproduction: none
task: BL-4235
tasks: BL-4235
found: 2026-10-29
found-at: ce4fb84772365b99da350fd339cc0c2e9cb2ce30
scorecard: 2026-10-29_0606.md
duplicate-of:
closed: 2026-10-31
closed-how: task-done-historical
closed-by: 2026-10-31_0358.md
---
# AF-0482 - BL-3563 cost $9.19 (8.6x the $1.07 median) over three opus runs, the last stopped at the 80-turn cap

## Summary

Low finding from the process auditor at `logs/BL-3563-20261028-052757-L1.jsonl`: BL-3563 cost $9.19 (8.6x the $1.07 median) over three opus runs, the last stopped at the 80-turn cap.

## Evidence

Location: `logs/BL-3563-20261028-052757-L1.jsonl`

Runs: BL-3563-20261026-063535-L5 (17 turns, 7.7 min), BL-3563-20261026-151632-L5 (62 turns, 33.7 min) and BL-3563-20261028-052757-L1 (81 turns, 53.3 min, stopped at the 80-turn cap; DarkFactory-20261028-052757-L1.log:53). Cost was driven by re-reading the task's scope on every claim and by a final run that hit the cap before the task was split. Two of the three runs ended on dependencies found mid-run. Deferred and split into BL-3976 to BL-3978.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).tasks | ? id -eq 'BL-3563'
```

- Expected: No task over 3x the median costUsd and over 5 USD.
- Actual: BL-3563 costUsd 9.1885, claims 3, outcome requeued.

## Re-audits

## Log

- 2026-10-29: filed proposed.
- 2026-10-29: proposed -> accepted.
- 2026-10-31: accepted -> closed. Audit 2026-10-31_0358.md: a cost-outlier finding records a past run's cost, which no fix can change; its tasks (BL-4235) are Done at bfe08061f844, so it closes without a re-audit (ADR-0422, amended 2026-10-14).
