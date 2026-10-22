---
id: BL-3079
title: Fix AF-0326: BL-2970 claimed 3 times and requeued 3 times: a file outside its touches, a stash that did not apply, then two cost-cap requeues, before it was deferred and split
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-22
completed:
---
# BL-3079 — Fix AF-0326: BL-2970 claimed 3 times and requeued 3 times: a file outside its touches, a stash that did not apply, then two cost-cap requeues, before it was deferred and split

## Goal

The defect the audit office reported as AF-0326 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0326 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0326-bl-2970-claimed-3-times-and-requeued-3-times-a-fil.md`.

Location: `logs/DarkFactory-20261021-064209-L1.log:356`

Location: `logs/DarkFactory-20261021-064209-L1.log:356`

Claim 1 (L1 of shift 064209): 12:47:17 to 13:02:52, 72 turns, $4.27. 'stash uncommitted work kept'. At 13:03:22 REQUEUE: 'Needs Curl.Engine.UnitTests (EngineCompositionDoh*Tests pin the 8.21.0 DoH lines), which BL-2921 in Doing touches; code is written and stashed'. Claim 2 (DarkFactory-20261021-140711-L8.log) at 14:39:09: 'stash 0725a3f9 from an earlier run did not apply cleanly'. It ran 13 turns ($0.87), then at 14:44:43 REQUEUE 'Cost cap: library side ported onto the BL-2981 split files'. Claim 3 (DarkFactory-20261021-150011-L2.log) at 17:41:45 applied stash 2b0064ac. It ran 5 turns ($0.46), then at 17:43:14 REQUEUE 'Cost cap: this claim cap of $1.02 cannot cover the trace and Engine tests...'. The task Log then records Backlog -> Deferred, split into BL-3011, BL-3012 and BL-3013. BL-3011 was itself PARKED at 20:58:14 ('push kept being refused'). The root cause was a touches list that missed a test file the change had to edit. Then the cost cap cut each later claim short. In total $5.60 was spent and BL-2970 itself finished nothing.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-19 -LogRoot Z:\repos\Curl.audit\20261022-013026\logs -CiRunsJson Z:\repos\Curl.audit\20261022-013026\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2970'; Select-String -Path Z:\repos\Curl.audit\20261022-013026\logs\DarkFactory-*-L*.log -Pattern 'BL-2970 +(claim|stash|REQUEUE|end)'
```

- Expected: BL-2970 claimed fewer than 3 times and requeued fewer than twice.
- Actual: claims=3, costUsd=5.602, outcome=requeued; 3 REQUEUE lines (outside touches, cost cap, cost cap); stash 0725a3f9 did not apply cleanly.

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
