---
id: BL-2280
title: Fix AF-0227: Quality gates job red for 26.83 minutes from run 38700283564 on 639a0aec, BL-2268's finish commit
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-10-15
completed:
---
# BL-2280 — Fix AF-0227: Quality gates job red for 26.83 minutes from run 38700283564 on 639a0aec, BL-2268's finish commit

## Goal

The defect the audit office reported as AF-0227 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0227 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0227-quality-gates-job-red-for-26-83-minutes-from-run-3.md`.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

The CI red rule is crossed (ciRedMinutes 170.52 over 60). Spell 2026-10-15T07:50:16Z to 08:17:06Z, 26.83 min, runId 38700283564: a failure on 639a0aec (chore(tasks): finish BL-2268) in job Quality gates (windows-latest), step Measure coverage, complexity and CRAP. A sibling run on the same sha, 38700277194, passed. The shift-end merge was withheld (DarkFactory-20261014-215526.log 00:50:41 'not merged: CI failure on 639a0ae'). BL-2269 was filed at 00:51:09 and finished at 01:07:03 ('Quality job failure was a lost coverage re...', DarkFactory-20261015-005118-L1.log). Run 38703393249 on 08cb9f24 turned green, and 08cb9f2 was merged to master at 01:18:03 after green, so master got no red merge. This is the second Quality gates measure-step failure in the window (with run 38666857143).

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-14 -LogRoot Z:\repos\Curl.audit\20261015-015611\logs -CiRunsJson Z:\repos\Curl.audit\20261015-015611\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Format-Table
```

- Expected: ciRedMinutes 60 or less.
- Actual: ciRedMinutes 170.52; spell 07:50:16Z to 08:17:06Z, 26.83 min, runId 38700283564.

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Interactive only: the finding and its reproduction tool sit in the audit folder, which the lane guard forbids reading, so a lane cannot see the finding or run the reproduction. The red spell itself (run 38700283564, a lost coverage report in the Measure step) was already fixed by BL-2269 (commit 6110cd360, Done); the recorded ciRedMinutes is history from logs and cannot change. Remove `lane: no` once an interactive session has read AF-0227, decided what in RunDarkFactory.ps1 (if anything) must change, and written that into this task, or closed it as the history-only case.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Backlog. Interactive only: the finding and its reproduction are in the audit folder, which lanes may not read; the red run's cause was fixed by BL-2269
