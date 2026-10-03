---
id: AF-0003
title: BL-564 was claimed 3 times: lanes 1, 4 and 6, with two conflict-resolve runs
auditor: process
severity: Medium
status: closed
reason: Re-audit 2026-10-03_1459.md: the reproduction no longer reproduces.
key: process:BL-564:BL-564:redone-work
task: BL-1069
found: 2026-09-30
found-at: d065d6d3507e2ed87905d40a24233af193913378
scorecard: 2026-09-30_1754.md
closed: 2026-10-03
closed-by: 2026-10-03_1459.md
---
# AF-0003 - BL-564 was claimed 3 times: lanes 1, 4 and 6, with two conflict-resolve runs

## Summary

Medium finding from the process auditor at `logs/BL-564-20260929-023709-L4.jsonl`: BL-564 was claimed 3 times: lanes 1, 4 and 6, with two conflict-resolve runs.

## Evidence

Location: `logs/BL-564-20260929-023709-L4.jsonl`

The tool reports claims=3 and costUsd=10.33 for BL-564. Lane 4's run ended 'FACTORY: BLOCKED BL-564 back in Backlog: its ADR folder Documentation/Planning/Decisions overlaps BL-883 in Doing'. Lane 6 later finished it ('FACTORY: DONE BL-564') after about 31 minutes. Lane 1 also ran it and finished DONE. Two '-resolve' runs (L1 and L6) then handled ADR-number renumbering conflicts. The shared ADR folder was the touches that serialised and duplicated the work. Overall, 35 tasks were claimed more than once and there were 40 requeues.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-09-23 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path ..\logs\BL-564-*.jsonl -Pattern 'FACTORY: (DONE|BLOCKED|RESOLVED) BL-564' -List
```

- Expected: Each task claimed once or twice and none requeued twice.
- Actual: BL-564 claims=3 across lanes 1, 4 and 6, with a BLOCKED then DONE sequence and two resolve runs.

## Re-audits

- 2026-10-02 | 2026-10-02_1400.md | reproduces: no | Ran the measure tool and the Select-String. BL-564 is not among the 244 tasks and ..\logs has no BL-564-* files, so Select-String finds nothing.
- 2026-10-03 | 2026-10-03_0623.md | reproduces: no | Ran the reproduction. The log folder holds 271 files, all from 2026-10-02 onward, and none are BL-564 logs. Select-String over BL-564-*.jsonl matched nothing. This is missing log data, not proof of a fix. The tool now reports tasksClaimedMoreThanOnce=5 (BL-1223, BL-1274, BL-1289, BL-1288, BL-1259), none of them BL-564.
- 2026-10-03 | 2026-10-03_1233.md | reproduces: no | The log folder holds only the 2026-10-03 shift. The tool output has no BL-564 task and the Select-String over ..\logs\BL-564-*.jsonl matched no files. Not reproducible on this log set.
- 2026-10-03 | 2026-10-03_1459.md | reproduces: no | The tool ran. BL-564 is not in the log folder; its tasks list holds only 2026-10-03 tasks and Select-String finds no BL-564 files.

## Log

- 2026-09-30: filed proposed.
- 2026-09-30: proposed -> accepted. Stewart accepted it on 2026-09-30 (log written 2026-10-02, BL-1183).
- 2026-10-03: accepted -> closed. Re-audit 2026-10-03_1459.md: the reproduction no longer reproduces.
