---
id: AF-0356
title: BL-2972 cost $1.92 over two claims; the first was requeued because it needed a file outside its touches
auditor: process
severity: Low
status: proposed
reason:
key: process:logs:BL-2972:cost-outlier
reproduction: none
task: none
tasks:
found: 2026-10-22
found-at: 608a755586679cf34031a0e5b137f01b9d7fb329
scorecard: 2026-10-22_1120.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0356 - BL-2972 cost $1.92 over two claims; the first was requeued because it needed a file outside its touches

## Summary

Low finding from the process auditor at `logs/BL-2972-20261022-024438-L3.jsonl`: BL-2972 cost $1.92 over two claims; the first was requeued because it needed a file outside its touches.

## Evidence

Location: `logs/BL-2972-20261022-024438-L3.jsonl`

First run (L3, 02:45:42-02:48:18): 17 turns, cost 0.5947, then 'REQUEUE ... Needs Curl.Networking.UnitLibrary/DohTr~' at 02:51:41. Second run (BL-2972-20261022-042722-L1.jsonl, 04:27:54-04:37:46, 'retry after a run that came back'): 43 turns, cost 1.3260. Its touches were too narrow, which cost about $0.59 of discarded work and a 2-hour delay.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261022-112013\logs -CiRunsJson Z:\repos\Curl.audit\20261022-112013\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2972'; Select-String -Path Z:\repos\Curl.audit\20261022-112013\logs\DarkFactory-20261022-*-L*.log -Pattern 'BL-2972 +(claim|REQUEUE|end)'
```

- Expected: costUsd at most 0.8106
- Actual: claims 2, minutes 11.12, costUsd 1.9207, done

## Re-audits

## Log

- 2026-10-22: filed proposed.
