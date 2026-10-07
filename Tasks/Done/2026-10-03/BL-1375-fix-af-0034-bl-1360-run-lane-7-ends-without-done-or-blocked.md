---
id: BL-1375
title: Fix AF-0034: BL-1360 run (lane 7) ends without DONE or BLOCKED after 87 minutes; BL-1325 blocked after 120 minutes
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1375 — Fix AF-0034: BL-1360 run (lane 7) ends without DONE or BLOCKED after 87 minutes; BL-1325 blocked after 120 minutes

## Goal

The defect the audit office reported as AF-0034 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0034 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0034-bl-1360-run-lane-7-ends-without-done-or-blocked-af.md`.

Location: `logs/DarkFactory-20261003-061205-L7.log`

Location: `logs/DarkFactory-20261003-061205-L7.log`

Tool outcome 'open' for BL-1360 (87.72 min, cost 0); L7's last line at 12:20:35 is a quality step. BL-1325 outcome 'blocked' (120.57 min, cost 0). I did not find the cause.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-03 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object outcome -ne 'done' | Select-Object id,outcome,minutes
```

- Expected: No open runs.
- Actual: BL-1360 open, BL-1325 blocked

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] A run killed at its `-TaskMinutes` deadline traces an `end` line in the lane log, and every task run's prompt names its kill time and limits `Measure-CodeQuality.ps1` to one run per library. (Was: "The finding's reproduction, run from the repository root, gives the expected result" - a lane may not read `Audit/`, and the 2026-10-03 logs it reads predate this fix; the process auditor's re-audit of a later shift checks it, see Notes.)
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause, from the lane logs and BL-1360's transcript: both runs hit the shift's
  120-minute run kill (`-TaskMinutes`). Under nine lanes, each `Measure-CodeQuality.ps1`
  call took 27 to 45 minutes (BL-1360 ran it five times, 11:13 to 13:05; BL-1325's test
  took 41 minutes and its quality run 45). The shift then blocked both with "timed out
  after 120 min". BL-1360 read as `open` because the killed run sends no `result` event,
  so the lane log had no `end` line for it, and the audit's log copy was taken at
  12:20, before the kill.
- Fix in `RunDarkFactory.ps1`: (1) both run prompts gain a rule giving the run its kill
  time (`{DEADLINE}`, `{MINUTES}`, filled in by `Invoke-TaskRun`), telling it to run
  `Measure-CodeQuality.ps1` once per changed library with `-ReportPath` and read the
  report, and to move an unfinishable task to Backlog before the deadline rather than be
  killed into Blocked; (2) a killed run now traces `end killed: timed out after N min`.
  Chose prompt guidance over raising `-TaskMinutes`: a longer limit only delays the kill.
- Not verified here: a lane may not read `Audit/` (guard-audit-paths), so the finding's
  reproduction was not run. It reads past logs, so it will keep listing these two runs
  for 2026-10-03; the process auditor's re-audit on a later shift's logs confirms it.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. Timed-out runs now trace an end line, and runs are told their kill time and to run Measure-CodeQuality once per library
