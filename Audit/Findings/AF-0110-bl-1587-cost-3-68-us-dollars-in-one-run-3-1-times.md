---
id: AF-0110
title: BL-1587 cost 3.68 US dollars in one run, 3.1 times the median, with three Opus sub-agents
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-10-09_0225.md: the reproduction no longer reproduces.
key: process:logs:BL-1587:cost-outlier
reproduction: none
task: BL-1788
tasks: BL-1788
found: 2026-10-08
found-at: 043959c94f40aaf4a1c37e70d0c6d957c5f1e564
scorecard: 2026-10-08_0748.md
duplicate-of:
closed: 2026-10-09
closed-how: reliable-reaudit
closed-by: 2026-10-09_0225.md
---
# AF-0110 - BL-1587 cost 3.68 US dollars in one run, 3.1 times the median, with three Opus sub-agents

## Summary

Low finding from the process auditor at `logs/BL-1587-20261007-111121-L8.jsonl`: BL-1587 cost 3.68 US dollars in one run, 3.1 times the median, with three Opus sub-agents.

## Evidence

Location: `logs/BL-1587-20261007-111121-L8.jsonl`

One run on L8: 8.3 min, 15 turns, 63 tool calls, 3 Agent calls; all opus, $3.68.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1587' | Select-Object id,claims,costUsd,minutes
```

- Expected: costUsd at most 3.57.
- Actual: BL-1587 claims 1 costUsd 3.6768

## Re-audits

- 2026-10-08 | 2026-10-08_2315.md | not re-audited | Ran the reproduction. There is no BL-1587 row; its runs are not in the copied log folder.
- 2026-10-09 | 2026-10-09_0225.md | reproduces: no | No cost outlier in the window: the most expensive task is BL-1818 at $1.49, 1.37 times the $1.09 median.

## Log

- 2026-10-08: filed proposed.
- 2026-10-08: proposed -> accepted. Stewart: "accept all findings".
- 2026-10-09: accepted -> closed. Re-audit 2026-10-09_0225.md: the reproduction no longer reproduces.
