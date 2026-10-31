---
id: AF-0469
title: BL-3754 claimed 3 times and requeued twice (files held by BL-4031/BL-4022, then a remainder split to BL-4067)
auditor: process
severity: Medium
status: accepted
reason: 
key: process:logs:BL-3754:redone-work
reproduction: none
task: BL-4187
tasks: BL-4187
found: 2026-10-29
found-at: ce4fb84772365b99da350fd339cc0c2e9cb2ce30
scorecard: 2026-10-29_0606.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0469 - BL-3754 claimed 3 times and requeued twice (files held by BL-4031/BL-4022, then a remainder split to BL-4067)

## Summary

Medium finding from the process auditor at `logs/DarkFactory-20261029-014637-L8.log:20`: BL-3754 claimed 3 times and requeued twice (files held by BL-4031/BL-4022, then a remainder split to BL-4067).

## Evidence

Location: `logs/DarkFactory-20261029-014637-L8.log:20`

Requeue 1 at 2026-10-28 22:31:41, before any edit: 'Needs ITransferEvents/Output/Networking files held by BL-4031 and BL-4022; touches corrected (Curl.Tls is not where the lines live)' (DarkFactory-20261028-201954-L6.log:90). Its touches named the wrong library, so the overlap check could not hold it back. Requeue 2 at 2026-10-29 02:28:51: 'remaining close_notify/shutdown/store-cache lines are BL-4067' (DarkFactory-20261029-014637-L8.log:20). Done on claim 3. The tool reports claims=3, cost $1.28.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; Select-String -Path Z:/repos/Curl.audit/20261029-060658/logs/DarkFactory-*-L*.log -Pattern 'BL-3754 (claim|REQUEUE|DONE)'
```

- Expected: Fewer than 3 claims and fewer than 2 requeues.
- Actual: BL-3754 claims=3, 2 REQUEUE lines.

## Re-audits

- 2026-10-31 | 2026-10-31_0358.md | reproduces: yes | Same cause: a held file, then a remainder split. BL-4328 was requeued at 22:16:33 ('Needs Curl.Console/CurlComposition.cs, held by BL-4320 in Doing'), requeued again at 00:07:54 on the 80-turn cap, and split into BL-4362..BL-4365 at 13:04:46 on 10-30. BL-4083 (CI fix) was also requeued on EngineComposition.cs held by BL-4082.

## Log

- 2026-10-29: filed proposed.
- 2026-10-29: proposed -> accepted.
