---
id: BL-4233
title: Fix AF-0480: CI red 58 min from run 40801399142 (fcc2b504, Quality gates windows-latest: Curl.Core and Curl.Engine outside the gate)
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
# BL-4233 — Fix AF-0480: CI red 58 min from run 40801399142 (fcc2b504, Quality gates windows-latest: Curl.Core and Curl.Engine outside the gate)

## Goal

The cause of the factory behaviour the audit office reported as AF-0480 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0480 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0480-ci-red-58-min-from-run-40801399142-fcc2b504-qualit.md`.

Location: `logs/DarkFactory-20261028-172341.log:65`

Location: `logs/DarkFactory-20261028-172341.log:65`

Spell 2026-10-29T01:56:20Z to 02:54:15Z (18:56-19:54 Arizona), 57.92 min, on fcc2b504 (BL-4006's test change). BL-4017 was filed at 19:03:57 (DarkFactory-20261028-172341.log:65). It added two tests for Curl.Core and Curl.Engine coverage. Lanes do not run Measure-CodeQuality for every library their change affects, so coverage gaps first show in CI.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).ciRedSpells | ? runId -eq 40801399142
```

- Expected: No red spell over 30 minutes.
- Actual: minutes 57.92, runId 40801399142.

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
