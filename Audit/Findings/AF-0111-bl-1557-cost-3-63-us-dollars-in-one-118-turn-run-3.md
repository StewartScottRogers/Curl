---
id: AF-0111
title: BL-1557 cost 3.63 US dollars in one 118-turn run, 3.05 times the median
auditor: process
severity: Low
status: accepted
reason:
key: process:logs:BL-1557:cost-outlier
reproduction: none
task: BL-1789
tasks: BL-1789
found: 2026-10-08
found-at: 043959c94f40aaf4a1c37e70d0c6d957c5f1e564
scorecard: 2026-10-08_0748.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0111 - BL-1557 cost 3.63 US dollars in one 118-turn run, 3.05 times the median

## Summary

Low finding from the process auditor at `logs/BL-1557-20261007-111121-L1.jsonl`: BL-1557 cost 3.63 US dollars in one 118-turn run, 3.05 times the median.

## Evidence

Location: `logs/BL-1557-20261007-111121-L1.jsonl`

One run on L1: 22.1 min, 118 turns (the most of any outlier), 117 tool calls, 0 Read; all opus, $3.63.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1557' | Select-Object id,claims,costUsd,minutes
```

- Expected: costUsd at most 3.57.
- Actual: BL-1557 claims 1 costUsd 3.6288

## Re-audits

## Log

- 2026-10-08: filed proposed.
- 2026-10-08: proposed -> accepted. Stewart: "accept all findings".
