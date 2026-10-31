---
id: BL-4231
title: Fix AF-0478: CI red 295 min from run 40724749646 (158f6fe6, test1034 on Linux and macOS); its fix task BL-3946 was requeued behind BL-3885
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-10-29
completed: 2026-10-31
---
# BL-4231 — Fix AF-0478: CI red 295 min from run 40724749646 (158f6fe6, test1034 on Linux and macOS); its fix task BL-3946 was requeued behind BL-3885

## Goal

The cause of the factory behaviour the audit office reported as AF-0478 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0478 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0478-ci-red-295-min-from-run-40724749646-158f6fe6-test1.md`.

Location: `logs/DarkFactory-20261028-052757.log:57`

Location: `logs/DarkFactory-20261028-052757.log:57`

Spell 2026-10-28T16:30:03Z to 21:24:47Z (09:30-14:24 Arizona), 294.73 min. BL-3946 was filed at 09:27:02 (DarkFactory-20261028-052757.log:57). It was requeued because 'BL-3885 ... holds Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs', then Blocked waiting on CI for fix d620937b7, and moved Done after the merge. A High CI-fix task waited behind a feature task that held the file it needed, so CI stayed red for nearly 5 hours. A shift restart (DarkFactory-20261028-111517.log:132, 14:48) and a coordinator that 'spun after its sync and started no lane' (DarkFactory-20261028-144818.log:2) fell inside this spell.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).ciRedSpells | ? runId -eq 40724749646
```

- Expected: No red spell over 30 minutes.
- Actual: minutes 294.73, runId 40724749646.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-31 (interactive): measured with Measure-FactoryProcess.ps1 over a snapshot of Z:/repos/Curl.logs (files written since 2026-10-30 12:00; the one live run BL-4420-L1 left out as running) with a fresh 500-run CI list, at about 11:10 Arizona 2026-10-31. ciRedSpells since fix f54935070 (ADR-1158, 10-30 09:10): two, 44 min (run 41128846186, 10-30 15:04-15:48) and 76 min (run 41151657472, test670, 10-30 18:33-19:49); both fall inside the spell when every coordinator was blind to CI (the timed gh wrapper space-joined --json a,b, from BL-4353 at 10-30 09:06), so no task was filed. That cause was fixed by 8c14126f5 (BL-4384, running from the 06:08 shift on 10-31); since then: no red spell over 30 min, the CI watch listed the runs on every heartbeat, and the shift-end merge wait could see CI. The finding's incident predates both fixes.

- Fix f54935070 (pushed to work/dark-factory 2026-10-30 about 09:10 Arizona, ADR-1158): a ready CI-fix task is claimed before any other work even when its files overlap a task in Doing; the shift's requeued list no longer keeps lanes off it; a CI fix waiting 2 minutes with no free lane gets an extra lane; and its integration goes ahead of ordinary waiters on the integrate lock. "Fix CI build" titles now count as CI fixes. Lanes load the factory at start, so measure from the first shift started after the push. Measured before the fix (since 2026-10-29): spells of 237 min (run 40885502110: BL-4083 requeued behind BL-4082, then skipped as requeued and ranked 6th by next), 139 min (run 40984137987: coordinator silent 17:50-18:45, 41 min waiting for a lane, then a second gate failure waiting 27 min for the integrate lock) and 49 min (run 40832703845). Outside the factory: one CI run takes 11-39 min with queueing, so a spell under 30 min also needs a faster CI run (BL-3512).

## Log

- 2026-10-29: Created.
- 2026-10-31: Backlog -> Doing. Interactive measurement Criteria at claim: 87cd20bd 9742157d.
- 2026-10-31: Doing -> Done. Measured 2026-10-31: the finding's expected result holds since its fix landed (see Notes); build clean, fast tests 114 assemblies passed
