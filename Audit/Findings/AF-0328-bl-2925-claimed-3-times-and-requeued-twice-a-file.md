---
id: AF-0328
title: BL-2925 claimed 3 times and requeued twice: a file outside its touches, then a used-up run cost budget, then blocked by the cost cap and split
auditor: process
severity: Medium
status: accepted
reason: 
key: process:logs:BL-2925:redone-work
reproduction: none
task: none
tasks:
found: 2026-10-22
found-at: 167e765b2a009a4887fc971075283ef9d24a73a1
scorecard: 2026-10-22_0130.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0328 - BL-2925 claimed 3 times and requeued twice: a file outside its touches, then a used-up run cost budget, then blocked by the cost cap and split

## Summary

Medium finding from the process auditor at `logs/DarkFactory-20261021-064209-L6.log:131`: BL-2925 claimed 3 times and requeued twice: a file outside its touches, then a used-up run cost budget, then blocked by the cost cap and split.

## Evidence

Location: `logs/DarkFactory-20261021-064209-L6.log:131`

Claim 1 (064209-L6): 10:07:48 to 10:15:43, 40 turns. At 10:17:52 REQUEUE 'Needs Curl.Engine.UnitLibrary/DebugCallbackTransferEvents.cs ... in the touches of BL-2934 (Doing)'. Claim 2 (150011-L1): 15:15:57 to 15:35:07, 44 turns, 19.1 min. Its work was stashed. At 15:37:20 REQUEUE 'Run cost budget used up with the work nearly done'. Claim 3 (175054-L2) at 17:51:42 was BLOCKED 19 seconds later: 'runs of the last day already cost 4.49 of its 5.48 US dollar cost cap'. The task Log ends Backlog -> Deferred, split into BL-3014 to BL-3017. This is the same pattern as AF-0304 and AF-0305: touches that miss a needed file, then the cost cap. Total cost $4.49.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-19 -LogRoot Z:\repos\Curl.audit\20261022-013026\logs -CiRunsJson Z:\repos\Curl.audit\20261022-013026\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2925'; Select-String -Path Z:\repos\Curl.audit\20261022-013026\logs\DarkFactory-*-L*.log -Pattern 'BL-2925 +(claim|stash|REQUEUE|BLOCKED)'
```

- Expected: BL-2925 claimed fewer than 3 times and requeued fewer than twice.
- Actual: claims=3, costUsd=4.4901, outcome=requeued; 2 REQUEUE lines and 1 BLOCKED (cost cap).

## Re-audits

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
