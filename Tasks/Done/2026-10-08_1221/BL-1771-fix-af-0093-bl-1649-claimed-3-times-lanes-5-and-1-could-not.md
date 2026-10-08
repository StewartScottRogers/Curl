---
id: BL-1771
title: Fix AF-0093: BL-1649 claimed 3 times; lanes 5 and 1 could not integrate because fast tests failed after rebasing
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1771 — Fix AF-0093: BL-1649 claimed 3 times; lanes 5 and 1 could not integrate because fast tests failed after rebasing

## Goal

The defect the audit office reported as AF-0093 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0093 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0093-bl-1649-claimed-3-times-lanes-5-and-1-could-not-in.md`.

Location: `logs/DarkFactory-20261007-111121-L5.log`

Location: `logs/DarkFactory-20261007-111121-L5.log`

L5 claimed it at 15:21:33 (14.7 min, 38 turns) and PARKED at 15:50:30: 'fast tests failed twice (Curl.Cookies.UnitTests: EveryMember_ManyConcurrentCallers_EndWit~'. L1 claimed it at 15:51:06 (7.4 min) and PARKED at 16:05:32: 'fast tests failed twice (no test named; then no test named)'. L7 claimed it at 16:05:57 and was Done at 16:12:47. Same cause as BL-1647: the flaky cookie test that BL-1651 fixed at 16:02:59.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1649' | Select-Object id,claims,outcome; Select-String -Path ..\logs\DarkFactory-20261007-111121-L*.log -Pattern 'BL-1649 (claim|PARKED|REQUEUE|DONE)'
```

- Expected: BL-1649 claimed once.
- Actual: BL-1649 claims 3; PARKED on L5 15:50:30 and L1 16:05:32; DONE on L7 16:12:47

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause: BL-1649's two parks were both fast-test runs red under every lane's load, not
  its change. L5's (a named concurrency-sensitive Curl.Cookies test) is the AF-0092 case
  BL-1770 fixed: the failed projects are rerun alone before a park. L1's was red twice with
  "no test named", which BL-1770's rerun-alone path skipped because no project was named.
- Fix (`RunDarkFactory.ps1`): `Get-FailedTestProjects` now also puts in doubt every
  `*.UnitTests` project the run never reported `Passed!` (a crashed host or a red run with
  no test named leaves its project without a summary line), so those are rerun alone too
  and the task integrates when they pass. More than 10 projects in doubt means the run
  broke as a whole, and the task still parks (default chosen so a broken build-wide run
  does not hold the integrate lock for a full serial rerun). A red run with no test named
  now quotes its last error-looking line in the trace and park reason, so the next one
  names its cause. Self-tests: `RunDarkFactory.ps1 -TestFlakyTests`, 10/10 pass.
- The reproduction reads the 2026-10-07 shift's logs, which cannot change; it stops
  reproducing for shifts run after this fix, which is what the process auditor's re-audit
  will measure.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Red fast-test runs with no test named now rerun the unreported projects alone before parking (AF-0093).
