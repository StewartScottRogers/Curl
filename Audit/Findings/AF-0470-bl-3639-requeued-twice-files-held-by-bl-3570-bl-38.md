---
id: AF-0470
title: BL-3639 requeued twice (files held by BL-3570/BL-3893, then the 80-turn cap) and then Deferred and split
auditor: process
severity: Medium
status: accepted
reason: 
key: process:logs:BL-3639:redone-work
reproduction: none
task: BL-4188
tasks: BL-4188
found: 2026-10-29
found-at: ce4fb84772365b99da350fd339cc0c2e9cb2ce30
scorecard: 2026-10-29_0606.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0470 - BL-3639 requeued twice (files held by BL-3570/BL-3893, then the 80-turn cap) and then Deferred and split

## Summary

Medium finding from the process auditor at `logs/DarkFactory-20261028-052757-L9.log:20`: BL-3639 requeued twice (files held by BL-3570/BL-3893, then the 80-turn cap) and then Deferred and split.

## Evidence

Location: `logs/DarkFactory-20261028-052757-L9.log:20`

Requeue 1 at 2026-10-26 17:55:48 after 27 turns: 'Waiting on BL-3570 (holds ...CommandLineOptionTable.cs) and BL-3893 (holds ...CommandLineRefusal.cs)' (DarkFactory-20261026-151632-L6.log:104). Requeue 2 at 2026-10-28 06:28:00: 81 turns in 14.2 min, stopped at the 80-turn cap (DarkFactory-20261028-052757-L9.log:20). The task was then Deferred and split into BL-3972, BL-3973 and BL-3974. Cost $5.30.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; Select-String -Path Z:/repos/Curl.audit/20261029-060658/logs/DarkFactory-*-L*.log -Pattern 'BL-3639 (claim|end|REQUEUE)'
```

- Expected: Fewer than 2 requeues.
- Actual: BL-3639: 2 REQUEUE lines, outcome requeued, then Deferred.

## Re-audits

## Log

- 2026-10-29: filed proposed.
- 2026-10-29: proposed -> accepted.
