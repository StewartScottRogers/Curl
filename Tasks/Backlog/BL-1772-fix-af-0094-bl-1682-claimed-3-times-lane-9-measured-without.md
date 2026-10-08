---
id: BL-1772
title: Fix AF-0094: BL-1682 claimed 3 times; lane 9 measured without fixing and lane 2 found it waiting on BL-1715, which its depends-on did not name
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-08
completed:
---
# BL-1772 — Fix AF-0094: BL-1682 claimed 3 times; lane 9 measured without fixing and lane 2 found it waiting on BL-1715, which its depends-on did not name

## Goal

The defect the audit office reported as AF-0094 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0094 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0094-bl-1682-claimed-3-times-lane-9-measured-without-fi.md`.

Location: `logs/DarkFactory-20261007-201432-L2.log`

Location: `logs/DarkFactory-20261007-201432-L2.log`

L9 claimed it at 20:51:04, ran 26.7 min and 48 turns ($1.79), and requeued at 21:20:34: 'Measured, not fixed ... more than this run's budget'. L2 claimed it at 21:33:51, ran 1.5 min ($0.73), and requeued at 21:38:14: 'Waits on BL-1715 (lazy per-scheme protocol handlers)'. A claim the board should not have made. L8 claimed it at 21:54:06 and was Done at 22:05:18 by another route (native curl.exe delay-loads DLLs). Total $3.76.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1682' | Select-Object id,claims,outcome,costUsd; Select-String -Path ..\logs\DarkFactory-20261007-201432-L*.log -Pattern 'BL-1682 (claim|REQUEUE|DONE)'
```

- Expected: BL-1682 claimed once.
- Actual: BL-1682 claims 3, costUsd 3.7582; REQUEUE on L9 21:20:34 and L2 21:38:14; DONE on L8 22:05:18

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
