---
id: AF-0052
title: BL-1458 cost 5.96 US dollars, 3.85 times the median run, and stopped at the per-task budget cap with its work stashed
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:BL-1458:cost-outlier
reproduction: none
task: none
tasks:
found: 2026-10-07
found-at: 5a627a2fb4baf7b4b2662dc309939ec576dcad20
scorecard: 2026-10-07_0844.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0052 - BL-1458 cost 5.96 US dollars, 3.85 times the median run, and stopped at the per-task budget cap with its work stashed

## Summary

Low finding from the process auditor at `logs/BL-1458-20261006-200306-L1.jsonl`: BL-1458 cost 5.96 US dollars, 3.85 times the median run, and stopped at the per-task budget cap with its work stashed.

## Evidence

Location: `logs/BL-1458-20261006-200306-L1.jsonl`

The median costUsd over the 143 measured tasks is 1.5508, so the outlier bar is 4.6524. BL-1458 has costUsd 5.9649, minutes 7.9, outcome blocked. The five costliest tasks are BL-1458 (5.96), BL-1462 (4.24), BL-1544 (3.57), BL-1433 (3.49) and BL-1450 (3.40); only BL-1458 is over the bar. What drove the cost: within 11 turns, DarkFactory-20261006-200306-L1.log:345-349 starts five parallel general-purpose subagents ('Diagnostics group A'..'E'). These made about 165 edit and write calls across about 30 Curl.Authentication.UnitTests files between 07:53:58 and 07:59:30. The run log has 29 Read calls, and up to 18 file_path references each to NtlmHttpAuthenticatorTests.cs, AuthDiagnosticLogTests.cs, SystemSecurityContextFactoryTests.cs and RoutingSecurityContextFactoryTests.cs. The run's result subtype is 'error_max_budget_usd'. L1.log:516 'stash uncommitted work kept' at 07:59:35, and the work is still in 'stash@{0}: On factory/lane-1: darkfactory BL-1458 20261006-200306'. The task was Blocked with 'split the task' (commit 0321532d6): the whole spend produced no integrated work.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-03 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Sort-Object costUsd -Descending | Select-Object -First 5 id,costUsd,minutes,outcome
```

- Expected: No task over 3 times the median costUsd (about 4.65).
- Actual: BL-1458 costUsd 5.9649, outcome blocked; the next is BL-1462 at 4.2366.

## Re-audits

- 2026-10-07 | 2026-10-07_1336.md | not re-audited | Ran the reproduction. The top 5 by cost are BL-1474 5.7678, BL-1525 5.254, BL-1473 4.9663, BL-1482 4.7552, BL-1470 4.7313. BL-1458 is absent from the tasks entirely, because the 2026-10-03 to 10-06 shift logs are not in the log folder. The result cannot tell whether it still reproduces; not re-audited.

## Log

- 2026-10-07: filed proposed.
- 2026-10-07: proposed -> accepted.
