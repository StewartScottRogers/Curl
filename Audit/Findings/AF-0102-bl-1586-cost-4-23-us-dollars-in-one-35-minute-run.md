---
id: AF-0102
title: BL-1586 cost 4.23 US dollars in one 35-minute run, 3.6 times the median
auditor: process
severity: Low
status: accepted
reason:
key: process:logs:BL-1586:cost-outlier
reproduction: none
task: BL-1780
tasks: BL-1780
found: 2026-10-08
found-at: 043959c94f40aaf4a1c37e70d0c6d957c5f1e564
scorecard: 2026-10-08_0748.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0102 - BL-1586 cost 4.23 US dollars in one 35-minute run, 3.6 times the median

## Summary

Low finding from the process auditor at `logs/BL-1586-20261007-111121-L2.jsonl`: BL-1586 cost 4.23 US dollars in one 35-minute run, 3.6 times the median.

## Evidence

Location: `logs/BL-1586-20261007-111121-L2.jsonl`

One run on L2: 35.3 min, 56 turns, 194 tool calls (the most of any outlier), 3 Agent calls, 50 'dotnet test' mentions; all opus, $4.23.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1586' | Select-Object id,claims,costUsd,minutes
```

- Expected: costUsd at most 3.57.
- Actual: BL-1586 claims 1 costUsd 4.2336

## Re-audits

## Log

- 2026-10-08: filed proposed.
- 2026-10-08: proposed -> accepted. Stewart: "accept all findings".
