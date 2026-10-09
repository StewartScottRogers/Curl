---
id: AF-0100
title: BL-1555 cost 4.33 US dollars in one run, 3.6 times the median, mostly in three Sonnet sub-agents
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-10-09_0225.md: the reproduction no longer reproduces.
key: process:logs:BL-1555:cost-outlier
reproduction: none
task: BL-1778
tasks: BL-1778
found: 2026-10-08
found-at: 043959c94f40aaf4a1c37e70d0c6d957c5f1e564
scorecard: 2026-10-08_0748.md
duplicate-of:
closed: 2026-10-09
closed-how: reliable-reaudit
closed-by: 2026-10-09_0225.md
---
# AF-0100 - BL-1555 cost 4.33 US dollars in one run, 3.6 times the median, mostly in three Sonnet sub-agents

## Summary

Low finding from the process auditor at `logs/BL-1555-20261007-111121-L1.jsonl`: BL-1555 cost 4.33 US dollars in one run, 3.6 times the median, mostly in three Sonnet sub-agents.

## Evidence

Location: `logs/BL-1555-20261007-111121-L1.jsonl`

One run on L1: 14.4 min, 28 turns, 104 tool calls, 3 Agent calls; claude-sonnet-5-5 $3.07 + opus $1.26.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1555' | Select-Object id,claims,costUsd,minutes
```

- Expected: costUsd at most 3.57.
- Actual: BL-1555 claims 1 costUsd 4.3333

## Re-audits

- 2026-10-08 | 2026-10-08_2315.md | not re-audited | Ran the reproduction. There is no BL-1555 row; its runs are not in the copied log folder.
- 2026-10-09 | 2026-10-09_0225.md | reproduces: no | No cost outlier in the window: the most expensive task is BL-1818 at $1.49, 1.37 times the $1.09 median.

## Log

- 2026-10-08: filed proposed.
- 2026-10-08: proposed -> accepted. Stewart: "accept all findings".
- 2026-10-09: accepted -> closed. Re-audit 2026-10-09_0225.md: the reproduction no longer reproduces.
