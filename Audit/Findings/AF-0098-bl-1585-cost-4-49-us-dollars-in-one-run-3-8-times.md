---
id: AF-0098
title: BL-1585 cost 4.49 US dollars in one run, 3.8 times the median, with three Opus test-writer sub-agents
auditor: process
severity: Low
status: proposed
reason:
key: process:logs:BL-1585:cost-outlier
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
# AF-0098 - BL-1585 cost 4.49 US dollars in one run, 3.8 times the median, with three Opus test-writer sub-agents

## Summary

Low finding from the process auditor at `logs/BL-1585-20261007-111121-L2.jsonl`: BL-1585 cost 4.49 US dollars in one run, 3.8 times the median, with three Opus test-writer sub-agents.

## Evidence

Location: `logs/BL-1585-20261007-111121-L2.jsonl`

One run on L2: 17 min, 19 turns, 93 tool calls, 3 Agent calls (test-writer for 1, 4 and 5 runner test files at 13:36); all opus, $4.49.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1585' | Select-Object id,claims,costUsd,minutes
```

- Expected: costUsd at most 3.57.
- Actual: BL-1585 claims 1 costUsd 4.4928

## Re-audits

## Log

- 2026-10-08: filed proposed.
