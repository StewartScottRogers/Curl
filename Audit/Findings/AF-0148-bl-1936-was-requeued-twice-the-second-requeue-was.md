---
id: AF-0148
title: BL-1936 was requeued twice; the second requeue was undone 16 seconds later and the task finished in the same run
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-10-11_0051.md: the reproduction no longer reproduces.
key: process:BL-1936:BL-1936:redone-work
reproduction: none
task: none
tasks:
found: 2026-10-10
found-at: 1b27494521dec4bdaa3fe60c8dc7a3fc73874253
scorecard: 2026-10-10_0123.md
duplicate-of:
closed: 2026-10-11
closed-how: reliable-reaudit
closed-by: 2026-10-11_0051.md
---
# AF-0148 - BL-1936 was requeued twice; the second requeue was undone 16 seconds later and the task finished in the same run

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261009-213949-L2.log:111`: BL-1936 was requeued twice; the second requeue was undone 16 seconds later and the task finished in the same run.

## Evidence

Location: `logs/DarkFactory-20261009-213949-L2.log:111`

The task Log shows two 'Doing -> Backlog' moves (rule: requeued twice). First, DarkFactory-20261009-162621-L2.log:553-559: claimed 21:05:21 and requeued 21:06:48 to split into BL-1937..BL-1941 (9 turns, $0.37). Second, DarkFactory-20261009-213949-L2.log:109-132: claimed 00:15:54. At 00:16:26 the run moved it to Backlog ('Depends on BL-1944 and BL-1940 (not Done)'), at 00:16:42 moved it back to Doing, and at 00:22:56 marked it DONE (31 turns, $1.13). So the dependency claim was false. Also BL-1945 closed at 00:26:03 as 'Already done by BL-1936', so its run was spent on work that was already done.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-09 -LogRoot Z:\repos\Curl.audit\20261010-012326\logs -CiRunsJson Z:\repos\Curl.audit\20261010-012326\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Tasks/Done/BL-1936-*.md -Pattern 'Doing -> Backlog'
```

- Expected: At most one Doing -> Backlog move for BL-1936
- Actual: Two Doing -> Backlog moves (2026-10-09 split; 2026-10-10 'Depends on BL-1944 and BL-1940 (not Done)', undone 16 s later)

## Re-audits

- 2026-10-11 | 2026-10-11_0051.md | reproduces: no | The given Select-String on Tasks/Done/BL-1936-*.md matches nothing because the file is now in Tasks/Done/2026-10-10_0525/. On that path it shows the two old requeues (2026-10-09 split; 2026-10-10 'Depends on BL-1944 and BL-1940'). Ran the rule over this window: requeues 10, and all 10 task-log 'Doing -> Backlog' lines dated 2026-10-10 in this window match lane REQUEUE lines. None was undone seconds later in the same run; the soonest re-claim after a requeue was hours later (BL-1975: 09:39 to 16:14; BL-2009: 18:51 to 21:57). The window has shifts that finished 82 tasks.

## Log

- 2026-10-10: filed proposed.
- 2026-10-11: proposed -> closed. Re-audit 2026-10-11_0051.md: the reproduction no longer reproduces.
