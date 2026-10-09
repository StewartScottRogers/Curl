---
id: AF-0105
title: BL-1499 cost 3.86 US dollars in one run, 3.2 times the median, with about 97 test-run mentions
auditor: process
severity: Low
status: accepted
reason:
key: process:logs:BL-1499:cost-outlier
reproduction: none
task: BL-1783
tasks: BL-1783
found: 2026-10-08
found-at: 043959c94f40aaf4a1c37e70d0c6d957c5f1e564
scorecard: 2026-10-08_0748.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0105 - BL-1499 cost 3.86 US dollars in one run, 3.2 times the median, with about 97 test-run mentions

## Summary

Low finding from the process auditor at `logs/BL-1499-20261007-111121-L4.jsonl`: BL-1499 cost 3.86 US dollars in one run, 3.2 times the median, with about 97 test-run mentions.

## Evidence

Location: `logs/BL-1499-20261007-111121-L4.jsonl`

One run on L4: 17 min of model time (36.7 min from claim to Done), 57 turns, 55 tool calls, 43 Bash, 97 'dotnet test' mentions in the run log; all opus, $3.86.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1499' | Select-Object id,claims,costUsd,minutes
```

- Expected: costUsd at most 3.57.
- Actual: BL-1499 claims 1 costUsd 3.8567

## Re-audits

- 2026-10-08 | 2026-10-08_2315.md | not re-audited | Ran the reproduction. There is no BL-1499 row; its runs are not in the copied log folder.

## Log

- 2026-10-08: filed proposed.
- 2026-10-08: proposed -> accepted. Stewart: "accept all findings".
