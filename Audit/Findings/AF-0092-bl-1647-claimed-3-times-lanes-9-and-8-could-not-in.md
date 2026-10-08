---
id: AF-0092
title: BL-1647 claimed 3 times; lanes 9 and 8 could not integrate because fast tests failed (the flaky EveryMember_ManyConcurrentCallers cookie test)
auditor: process
severity: Medium
status: accepted
reason:
key: process:logs:BL-1647:redone-work
reproduction: none
task: BL-1770
tasks: BL-1770
found: 2026-10-08
found-at: 043959c94f40aaf4a1c37e70d0c6d957c5f1e564
scorecard: 2026-10-08_0748.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0092 - BL-1647 claimed 3 times; lanes 9 and 8 could not integrate because fast tests failed (the flaky EveryMember_ManyConcurrentCallers cookie test)

## Summary

Medium finding from the process auditor at `logs/DarkFactory-20261007-111121-L8.log`: BL-1647 claimed 3 times; lanes 9 and 8 could not integrate because fast tests failed (the flaky EveryMember_ManyConcurrentCallers cookie test).

## Evidence

Location: `logs/DarkFactory-20261007-111121-L8.log`

L9 claimed it at 15:10:36 and PARKED at 15:34:12: 'fast tests failed twice (Curl.Cookies.UnitTests failed; then no test named) after rebasing'. L8 claimed it at 15:45:09 and PARKED at 16:05:17: 'fast tests failed twice (EveryMember_ManyConcurrentCallers_EndWithTheSameCookiesAsOneAfterAnother; then Curl.Cookies.UnitTests failed)'. L1 claimed it at 16:05:41 and was Done at 16:17:58, after BL-1651 fixed that test (integrated at 16:02:59). In the same 15:28-16:20 window, lanes could not integrate BL-1649, BL-1653, BL-1627, BL-1619, BL-1588, BL-1500, BL-1498 and BL-1632 either; for most, the cookie test or 'no test named' was the failure.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1647' | Select-Object id,claims,outcome; Select-String -Path ..\logs\DarkFactory-20261007-111121-L*.log -Pattern 'BL-1647 (claim|PARKED|REQUEUE|DONE)'
```

- Expected: BL-1647 claimed once.
- Actual: BL-1647 claims 3; PARKED on L9 15:34:12 and L8 16:05:17; DONE on L1 16:17:58

## Re-audits

## Log

- 2026-10-08: filed proposed.
- 2026-10-08: proposed -> accepted. Stewart: "accept all findings".
