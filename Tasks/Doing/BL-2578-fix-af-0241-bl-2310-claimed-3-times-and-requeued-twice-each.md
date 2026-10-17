---
id: BL-2578
title: Fix AF-0241: BL-2310 claimed 3 times and requeued twice: each run found its prerequisites still incomplete and filed more subtasks
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-17
completed:
---
# BL-2578 — Fix AF-0241: BL-2310 claimed 3 times and requeued twice: each run found its prerequisites still incomplete and filed more subtasks

## Goal

The defect the audit office reported as AF-0241 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0241 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0241-bl-2310-claimed-3-times-and-requeued-twice-each-ru.md`.

Location: `logs/DarkFactory-20261015-062423-L3.log:616`

Location: `logs/DarkFactory-20261015-062423-L3.log:616`

Measure-FactoryProcess.ps1 (-Since 2026-10-15): BL-2310 claims 3. Lane traces (local time, UTC-7): L3 claim 12:41:01, build FAIL 83 error(s), REQUEUE 12:42:52 'Engine still uses CommandLineOptions in 14 files; waits on BL-2325, BL-2338, BL-2343, BL-2350' (DarkFactory-20261015-062423-L3.log:604-616); L2 claim 14:41:26, REQUEUE 14:45:09 'in 10 files; waits on BL-2355..BL-2358' (DarkFactory-20261015-144052-L2.log:2-16); L1 claim 16:20:31, DONE 16:26:41 (DarkFactory-20261015-162014-L1.log:2-20). Run costs $0.52 (21 turns), $1.08 (26 turns), $1.22 (27 turns): the first two runs ($1.60) produced only a requeue. Why: an umbrella task ('remove the engine's reference to Curl.Cli') became ready before its remaining work was split out; its depends-on list grew from the first requeue to 18 tasks, and each claim discovered the next layer of remaining files instead of the planner filing all of them up front. The task Log agrees with the run logs.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-15 -LogRoot Z:\repos\Curl.audit\20261016-162347\logs -CiRunsJson Z:\repos\Curl.audit\20261016-162347\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | ? id -eq 'BL-2310'; Select-String -Path Z:\repos\Curl.audit\20261016-162347\logs\DarkFactory-*-L*.log -Pattern 'BL-2310 (claim|REQUEUE|DONE)'
```

- Expected: BL-2310 claimed once or twice, never requeued twice.
- Actual: BL-2310 claims 3; two REQUEUE lines (12:42:52 and 14:45:09) before DONE at 16:26:41.

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
