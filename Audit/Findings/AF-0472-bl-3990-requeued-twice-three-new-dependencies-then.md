---
id: AF-0472
title: BL-3990 requeued twice (three new dependencies, then the cost cap) and then Deferred and split
auditor: process
severity: Medium
status: closed
reason: Re-audit 2026-10-31_1149.md: the reproduction no longer reproduces.
key: process:logs:BL-3990:redone-work
reproduction: none
task: BL-4227
tasks: BL-4227
found: 2026-10-29
found-at: ce4fb84772365b99da350fd339cc0c2e9cb2ce30
scorecard: 2026-10-29_0606.md
duplicate-of:
closed: 2026-10-31
closed-how: reliable-reaudit
closed-by: 2026-10-31_1149.md
---
# AF-0472 - BL-3990 requeued twice (three new dependencies, then the cost cap) and then Deferred and split

## Summary

Medium finding from the process auditor at `logs/DarkFactory-20261029-014637-L1.log:15`: BL-3990 requeued twice (three new dependencies, then the cost cap) and then Deferred and split.

## Evidence

Location: `logs/DarkFactory-20261029-014637-L1.log:15`

Requeue 1 at 2026-10-28 21:30:06: 'Needs BL-4036 ..., BL-4037 ... and BL-4038 ...' (DarkFactory-20261028-201954-L6.log:35). Requeue 2 at 2026-10-29 01:52:02: 'Cost cap: the plan and decisions are under Notes' (DarkFactory-20261029-014637-L1.log:15). The task was then Deferred and split into BL-4068 and BL-4069. The tool reports claims=2, cost $2.41.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; Select-String -Path Z:/repos/Curl.audit/20261029-060658/logs/DarkFactory-*-L*.log -Pattern 'BL-3990 (claim|REQUEUE)'
```

- Expected: Fewer than 2 requeues.
- Actual: BL-3990: 2 REQUEUE lines, outcome requeued, then Deferred.

## Re-audits

- 2026-10-31 | 2026-10-31_0358.md | reproduces: yes | BL-4112 was requeued at 10:18:11 on three new dependencies (BL-4196, BL-4194, BL-4195), then at 16:25:53 on the 80-turn cap, then Deferred ('Split into BL-4298, BL-4295, BL-4296 and BL-4297; cost cap exceeded'). BL-4170 was also Blocked on its cost cap after two requeues.
- 2026-10-31 | 2026-10-31_1149.md | reproduces: no | By mechanism over this window: requeues 0. BL-4377 met its cost cap but was not claimed in this window; it got one split run (06:07-06:08), and both parts were done in one claim each.

## Log

- 2026-10-29: filed proposed.
- 2026-10-29: proposed -> accepted.
- 2026-10-31: accepted -> closed. Re-audit 2026-10-31_1149.md: the reproduction no longer reproduces.
