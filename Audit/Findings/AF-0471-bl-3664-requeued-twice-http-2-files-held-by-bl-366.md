---
id: AF-0471
title: BL-3664 requeued twice (HTTP/2 files held by BL-3667/BL-3668, then BL-3979 with the fix stashed) and then Deferred and split
auditor: process
severity: Medium
status: closed
reason: Re-audit 2026-10-31_1149.md: the reproduction no longer reproduces.
key: process:logs:BL-3664:redone-work
reproduction: none
task: BL-4189
tasks: BL-4189
found: 2026-10-29
found-at: ce4fb84772365b99da350fd339cc0c2e9cb2ce30
scorecard: 2026-10-29_0606.md
duplicate-of:
closed: 2026-10-31
closed-how: reliable-reaudit
closed-by: 2026-10-31_1149.md
---
# AF-0471 - BL-3664 requeued twice (HTTP/2 files held by BL-3667/BL-3668, then BL-3979 with the fix stashed) and then Deferred and split

## Summary

Medium finding from the process auditor at `logs/DarkFactory-20261028-111517-L2.log:53`: BL-3664 requeued twice (HTTP/2 files held by BL-3667/BL-3668, then BL-3979 with the fix stashed) and then Deferred and split.

## Evidence

Location: `logs/DarkFactory-20261028-111517-L2.log:53`

Requeue 1 at 2026-10-26 18:58:55: 'Needs ...Http2StreamConnection.cs (held by BL-3667) and Http2Session.cs (held by BL-3668)' (DarkFactory-20261026-151632-L3.log:117). Requeue 2 at 2026-10-28 11:55:59: 'Waits on BL-3979 ... every other CGR-135 repro now matches REF, fix stashed' (DarkFactory-20261028-111517-L2.log:53). The task was then Deferred and split into BL-4004 and BL-4005. Work left in a stash risks being lost. Cost $3.76.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; Select-String -Path Z:/repos/Curl.audit/20261029-060658/logs/DarkFactory-*-L*.log -Pattern 'BL-3664 (claim|REQUEUE)'
```

- Expected: Fewer than 2 requeues.
- Actual: BL-3664: 2 REQUEUE lines, outcome requeued, then Deferred.

## Re-audits

- 2026-10-31 | 2026-10-31_0358.md | reproduces: yes | Requeues with the fix stashed were followed by Deferred and split. BL-4131 was requeued at 08:09:38 ('Shift deadline: code done and 99 tests pass'; result: 'uncommitted for the shift to stash'), then Deferred and split into BL-4373/BL-4374. BL-4132 was requeued at 08:10:29 ('fixed in the stash') and BL-4137 at 08:11:56 ('uncommitted, stashed'); both needed a third claim.
- 2026-10-31 | 2026-10-31_1149.md | reproduces: no | By mechanism over this window: requeues 0, tasksClaimedMoreThanOnce 0. The only stash line is BL-4384 applying an earlier run's stash 9cace81d and finishing Done in one claim.

## Log

- 2026-10-29: filed proposed.
- 2026-10-29: proposed -> accepted.
- 2026-10-31: accepted -> closed. Re-audit 2026-10-31_1149.md: the reproduction no longer reproduces.
