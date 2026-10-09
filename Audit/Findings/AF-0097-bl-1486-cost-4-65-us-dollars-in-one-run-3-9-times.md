---
id: AF-0097
title: BL-1486 cost 4.65 US dollars in one run, 3.9 times the median, mostly in five Sonnet sub-agents
auditor: process
severity: Low
status: accepted
reason:
key: process:logs:BL-1486:cost-outlier
reproduction: none
task: BL-1775
tasks: BL-1775
found: 2026-10-08
found-at: 043959c94f40aaf4a1c37e70d0c6d957c5f1e564
scorecard: 2026-10-08_0748.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0097 - BL-1486 cost 4.65 US dollars in one run, 3.9 times the median, mostly in five Sonnet sub-agents

## Summary

Low finding from the process auditor at `logs/BL-1486-20261007-111121-L7.jsonl`: BL-1486 cost 4.65 US dollars in one run, 3.9 times the median, mostly in five Sonnet sub-agents.

## Evidence

Location: `logs/BL-1486-20261007-111121-L7.jsonl`

One run on L7: 15.7 min, 38 turns, 125 tool calls (66 Bash, 24 Read), 5 Agent calls; claude-sonnet-5-5 $2.97 + opus $1.68.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1486' | Select-Object id,claims,costUsd,minutes
```

- Expected: costUsd at most 3.57.
- Actual: BL-1486 claims 1 costUsd 4.6491

## Re-audits

- 2026-10-08 | 2026-10-08_2315.md | not re-audited | Ran the reproduction. There is no BL-1486 row; its runs are not in the copied log folder.

## Log

- 2026-10-08: filed proposed.
- 2026-10-08: proposed -> accepted. Stewart: "accept all findings".
