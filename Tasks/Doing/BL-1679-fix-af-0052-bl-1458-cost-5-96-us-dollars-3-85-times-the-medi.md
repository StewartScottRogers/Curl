---
id: BL-1679
title: Fix AF-0052: BL-1458 cost 5.96 US dollars, 3.85 times the median run, and stopped at the per-task budget cap with its work stashed
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-08
completed:
---
# BL-1679 — Fix AF-0052: BL-1458 cost 5.96 US dollars, 3.85 times the median run, and stopped at the per-task budget cap with its work stashed

## Goal

The defect the audit office reported as AF-0052 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0052 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0052-bl-1458-cost-5-96-us-dollars-3-85-times-the-median.md`.

Location: `logs/BL-1458-20261006-200306-L1.jsonl`

Location: `logs/BL-1458-20261006-200306-L1.jsonl`

The median costUsd over the 143 measured tasks is 1.5508, so the outlier bar is 4.6524. BL-1458 has costUsd 5.9649, minutes 7.9, outcome blocked. The five costliest tasks are BL-1458 (5.96), BL-1462 (4.24), BL-1544 (3.57), BL-1433 (3.49) and BL-1450 (3.40); only BL-1458 is over the bar. What drove the cost: within 11 turns, DarkFactory-20261006-200306-L1.log:345-349 starts five parallel general-purpose subagents ('Diagnostics group A'..'E'). These made about 165 edit and write calls across about 30 Curl.Authentication.UnitTests files between 07:53:58 and 07:59:30. The run log has 29 Read calls, and up to 18 file_path references each to NtlmHttpAuthenticatorTests.cs, AuthDiagnosticLogTests.cs, SystemSecurityContextFactoryTests.cs and RoutingSecurityContextFactoryTests.cs. The run's result subtype is 'error_max_budget_usd'. L1.log:516 'stash uncommitted work kept' at 07:59:35, and the work is still in 'stash@{0}: On factory/lane-1: darkfactory BL-1458 20261006-200306'. The task was Blocked with 'split the task' (commit 0321532d6): the whole spend produced no integrated work.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-03 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Sort-Object costUsd -Descending | Select-Object -First 5 id,costUsd,minutes,outcome
```

- Expected: No task over 3 times the median costUsd (about 4.65).
- Actual: BL-1458 costUsd 5.9649, outcome blocked; the next is BL-1462 at 4.2366.

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
