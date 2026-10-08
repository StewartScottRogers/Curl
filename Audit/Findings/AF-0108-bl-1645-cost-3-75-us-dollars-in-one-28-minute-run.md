---
id: AF-0108
title: BL-1645 cost 3.75 US dollars in one 28-minute run, 3.1 times the median
auditor: process
severity: Low
status: accepted
reason:
key: process:logs:BL-1645:cost-outlier
reproduction: none
task: BL-1786
tasks: BL-1786
found: 2026-10-08
found-at: 043959c94f40aaf4a1c37e70d0c6d957c5f1e564
scorecard: 2026-10-08_0748.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0108 - BL-1645 cost 3.75 US dollars in one 28-minute run, 3.1 times the median

## Summary

Low finding from the process auditor at `logs/BL-1645-20261007-111121-L4.jsonl`: BL-1645 cost 3.75 US dollars in one 28-minute run, 3.1 times the median.

## Evidence

Location: `logs/BL-1645-20261007-111121-L4.jsonl`

One run on L4: 27.7 min, 51 turns, 45 Bash calls, 41 'dotnet test' mentions; all opus, $3.75. This is the follow-on to BL-1525's 5x curve speed target.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1645' | Select-Object id,claims,costUsd,minutes
```

- Expected: costUsd at most 3.57.
- Actual: BL-1645 claims 1 costUsd 3.7467

## Re-audits

## Log

- 2026-10-08: filed proposed.
- 2026-10-08: proposed -> accepted. Stewart: "accept all findings".
