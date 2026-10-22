---
id: BL-3081
title: Fix AF-0328: BL-2925 claimed 3 times and requeued twice: a file outside its touches, then a used-up run cost budget, then blocked by the cost cap and split
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/Requeue.ps1]
requirement: none
created: 2026-10-22
completed:
---
# BL-3081 — Fix AF-0328: BL-2925 claimed 3 times and requeued twice: a file outside its touches, then a used-up run cost budget, then blocked by the cost cap and split

## Goal

The defect the audit office reported as AF-0328 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0328 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0328-bl-2925-claimed-3-times-and-requeued-twice-a-file.md`.

Location: `logs/DarkFactory-20261021-064209-L6.log:131`

Location: `logs/DarkFactory-20261021-064209-L6.log:131`

Claim 1 (064209-L6): 10:07:48 to 10:15:43, 40 turns. At 10:17:52 REQUEUE 'Needs Curl.Engine.UnitLibrary/DebugCallbackTransferEvents.cs ... in the touches of BL-2934 (Doing)'. Claim 2 (150011-L1): 15:15:57 to 15:35:07, 44 turns, 19.1 min. Its work was stashed. At 15:37:20 REQUEUE 'Run cost budget used up with the work nearly done'. Claim 3 (175054-L2) at 17:51:42 was BLOCKED 19 seconds later: 'runs of the last day already cost 4.49 of its 5.48 US dollar cost cap'. The task Log ends Backlog -> Deferred, split into BL-3014 to BL-3017. This is the same pattern as AF-0304 and AF-0305: touches that miss a needed file, then the cost cap. Total cost $4.49.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-19 -LogRoot Z:\repos\Curl.audit\20261022-013026\logs -CiRunsJson Z:\repos\Curl.audit\20261022-013026\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2925'; Select-String -Path Z:\repos\Curl.audit\20261022-013026\logs\DarkFactory-*-L*.log -Pattern 'BL-2925 +(claim|stash|REQUEUE|BLOCKED)'
```

- Expected: BL-2925 claimed fewer than 3 times and requeued fewer than twice.
- Actual: claims=3, costUsd=4.4901, outcome=requeued; 2 REQUEUE lines and 1 BLOCKED (cost cap).

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-22: Created.
