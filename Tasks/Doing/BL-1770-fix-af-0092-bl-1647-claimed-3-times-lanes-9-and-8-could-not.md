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
completed:
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

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
