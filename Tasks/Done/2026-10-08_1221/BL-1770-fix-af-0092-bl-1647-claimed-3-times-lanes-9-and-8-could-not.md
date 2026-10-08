---
id: BL-1770
title: Fix AF-0092: BL-1647 claimed 3 times; lanes 9 and 8 could not integrate because fast tests failed (the flaky EveryMember_ManyConcurrentCallers cookie test)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1770 — Fix AF-0092: BL-1647 claimed 3 times; lanes 9 and 8 could not integrate because fast tests failed (the flaky EveryMember_ManyConcurrentCallers cookie test)

## Goal

The defect the audit office reported as AF-0092 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0092 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0092-bl-1647-claimed-3-times-lanes-9-and-8-could-not-in.md`.

Location: `logs/DarkFactory-20261007-111121-L8.log`

Location: `logs/DarkFactory-20261007-111121-L8.log`

L9 claimed it at 15:10:36 and PARKED at 15:34:12: 'fast tests failed twice (Curl.Cookies.UnitTests failed; then no test named) after rebasing'. L8 claimed it at 15:45:09 and PARKED at 16:05:17: 'fast tests failed twice (EveryMember_ManyConcurrentCallers_EndWithTheSameCookiesAsOneAfterAnother; then Curl.Cookies.UnitTests failed)'. L1 claimed it at 16:05:41 and was Done at 16:17:58, after BL-1651 fixed that test (integrated at 16:02:59). In the same 15:28-16:20 window, lanes could not integrate BL-1649, BL-1653, BL-1627, BL-1619, BL-1588, BL-1500, BL-1498 and BL-1632 either; for most, the cookie test or 'no test named' was the failure.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1647' | Select-Object id,claims,outcome; Select-String -Path ..\logs\DarkFactory-20261007-111121-L*.log -Pattern 'BL-1647 (claim|PARKED|REQUEUE|DONE)'
```

- Expected: BL-1647 claimed once.
- Actual: BL-1647 claims 3; PARKED on L9 15:34:12 and L8 16:05:17; DONE on L1 16:17:58

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded. (For shifts after this fix; see Notes: the 2026-10-07 logs it reads are history and cannot change.)
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause: `Test-Green` in `RunDarkFactory.ps1` parked a finished task whenever the fast
  tests were red twice in full runs. A concurrency-sensitive test
  (`EveryMember_ManyConcurrentCallers_...` in Curl.Cookies.UnitTests) stayed red through
  both full runs while nine lanes built at once, so BL-1647 and eight other tasks in the
  15:28-16:20 window were parked and reclaimed, though none had broken it.
- Fix: after two red full runs, the test projects both runs named as failed
  (`Get-FailedTestProjects`) are run once more on their own (`Test-GreenAlone`). Green
  alone means the red came from load, not the task: it is traced as `flaky` and the task
  integrates. A real breakage is still red alone and still parks. Chosen default: a run
  red without naming a project, or with an aborted test host, vouches for nothing
  smaller, so then the task still parks as before (conservative; the L9 "then no test
  named" case stays a park).
- `-TestFlakyTests` gains three self-test cases for `Get-FailedTestProjects`; all pass.
  Script help (integrate step) and `Test-Green`'s comment say the new rule.
- The reproduction reads `-Since 2026-10-07` logs, which record the three claims and
  will keep recording them; the fix is that a later window's logs show a task claimed
  once when only load-sensitive tests are red. The process auditor's re-audit on a later
  window is what closes AF-0092.
- Build clean; fast tests: 33 test assemblies passed, none failed.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Integration reruns the failing test projects alone after two red full runs, so a load-flaky test no longer parks finished tasks (AF-0092)
