---
id: AF-0095
title: BL-1488 cost 6.56 US dollars, 5.5 times the 1.19 median; lane 7's five Sonnet sub-agents used up the session budget
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-10-09_0225.md: the reproduction no longer reproduces.
key: process:logs:BL-1488:cost-outlier
reproduction: none
task: BL-1773
tasks: BL-1773
found: 2026-10-08
found-at: 043959c94f40aaf4a1c37e70d0c6d957c5f1e564
scorecard: 2026-10-08_0748.md
duplicate-of:
closed: 2026-10-09
closed-how: reliable-reaudit
closed-by: 2026-10-09_0225.md
---
# AF-0095 - BL-1488 cost 6.56 US dollars, 5.5 times the 1.19 median; lane 7's five Sonnet sub-agents used up the session budget

## Summary

Low finding from the process auditor at `logs/BL-1488-20261007-111121-L7.jsonl`: BL-1488 cost 6.56 US dollars, 5.5 times the 1.19 median; lane 7's five Sonnet sub-agents used up the session budget.

## Evidence

Location: `logs/BL-1488-20261007-111121-L7.jsonl`

L7 run: 21.4 min, 184 tool calls, 5 Agent calls; $5.73, of which claude-sonnet-5-5 $4.86 and opus $0.87. It ended at 14:42:22 with stash and REQUEUE 'Session budget ran out with work in the~'. L2 then did the task in 4.8 min and 24 turns for $0.83 (DONE 14:52:01).

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Sort-Object costUsd -Descending | Select-Object -First 5 id,claims,costUsd,minutes,outcome
```

- Expected: No task over 3 times the median costUsd (3.57).
- Actual: BL-1488 costUsd 6.5614 (claims 2)

## Re-audits

- 2026-10-08 | 2026-10-08_2315.md | not re-audited | Ran the reproduction. The top 5 since 2026-10-07 are BL-1858, BL-1833, BL-1730, BL-1735 and BL-1846 (all 1 claim, at most 2.54 USD; median 1.07). BL-1488's runs predate the copied logs, so this could not tell.
- 2026-10-09 | 2026-10-09_0225.md | reproduces: no | Window's costliest tasks: BL-1818 $1.49, BL-1859 $1.19, BL-1850 $0.99, BL-1849 $0.51. The median is $1.09 and none exceeds 3 times it.

## Log

- 2026-10-08: filed proposed.
- 2026-10-08: proposed -> accepted. Stewart: "accept all findings".
- 2026-10-09: accepted -> closed. Re-audit 2026-10-09_0225.md: the reproduction no longer reproduces.
