---
id: AF-0104
title: BL-1487 cost 3.88 US dollars in one run, 3.3 times the median, mostly in four Sonnet sub-agents
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-10-09_0225.md: the reproduction no longer reproduces.
key: process:logs:BL-1487:cost-outlier
reproduction: none
task: BL-1782
tasks: BL-1782
found: 2026-10-08
found-at: 043959c94f40aaf4a1c37e70d0c6d957c5f1e564
scorecard: 2026-10-08_0748.md
duplicate-of:
closed: 2026-10-09
closed-how: reliable-reaudit
closed-by: 2026-10-09_0225.md
---
# AF-0104 - BL-1487 cost 3.88 US dollars in one run, 3.3 times the median, mostly in four Sonnet sub-agents

## Summary

Low finding from the process auditor at `logs/BL-1487-20261007-111121-L6.jsonl`: BL-1487 cost 3.88 US dollars in one run, 3.3 times the median, mostly in four Sonnet sub-agents.

## Evidence

Location: `logs/BL-1487-20261007-111121-L6.jsonl`

One run on L6: 17.8 min, 27 turns, 98 tool calls, 4 Agent calls, 9 'has been denied' messages; claude-sonnet-5-5 $2.58 + opus $1.30.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1487' | Select-Object id,claims,costUsd,minutes
```

- Expected: costUsd at most 3.57.
- Actual: BL-1487 claims 1 costUsd 3.8816

## Re-audits

- 2026-10-08 | 2026-10-08_2315.md | not re-audited | Ran the reproduction. There is no BL-1487 row; its runs are not in the copied log folder.
- 2026-10-09 | 2026-10-09_0225.md | reproduces: no | No cost outlier in the window: the most expensive task is BL-1818 at $1.49, 1.37 times the $1.09 median.

## Log

- 2026-10-08: filed proposed.
- 2026-10-08: proposed -> accepted. Stewart: "accept all findings".
- 2026-10-09: accepted -> closed. Re-audit 2026-10-09_0225.md: the reproduction no longer reproduces.
