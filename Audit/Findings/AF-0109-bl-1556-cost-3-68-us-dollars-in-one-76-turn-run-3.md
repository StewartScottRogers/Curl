---
id: AF-0109
title: BL-1556 cost 3.68 US dollars in one 76-turn run, 3.1 times the median
auditor: process
severity: Low
status: proposed
reason:
key: process:logs:BL-1556:cost-outlier
reproduction: none
task: none
tasks:
found: 2026-10-08
found-at: 043959c94f40aaf4a1c37e70d0c6d957c5f1e564
scorecard: 2026-10-08_0748.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0109 - BL-1556 cost 3.68 US dollars in one 76-turn run, 3.1 times the median

## Summary

Low finding from the process auditor at `logs/BL-1556-20261007-111121-L1.jsonl`: BL-1556 cost 3.68 US dollars in one 76-turn run, 3.1 times the median.

## Evidence

Location: `logs/BL-1556-20261007-111121-L1.jsonl`

One run on L1: 23.1 min, 76 turns, 74 tool calls, 38 'dotnet test' mentions; all opus, $3.68.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1556' | Select-Object id,claims,costUsd,minutes
```

- Expected: costUsd at most 3.57.
- Actual: BL-1556 claims 1 costUsd 3.6839

## Re-audits

## Log

- 2026-10-08: filed proposed.
