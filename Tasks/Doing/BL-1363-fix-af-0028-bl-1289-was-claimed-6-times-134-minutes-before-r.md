---
id: BL-1363
title: Fix AF-0028: BL-1289 was claimed 6 times (134 minutes) before reaching Done
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-03
completed:
---
# BL-1363 — Fix AF-0028: BL-1289 was claimed 6 times (134 minutes) before reaching Done

## Goal

The defect the audit office reported as AF-0028 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0028 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0028-bl-1289-was-claimed-6-times-134-minutes-before-rea.md`.

Location: `logs/BL-1289-20261002-211047-L1.jsonl:1`

Location: `logs/BL-1289-20261002-211047-L1.jsonl:1`

Measure-FactoryProcess.ps1 -Since 2026-09-30 reports BL-1289 with claims=6, minutes=134.62, costUsd=4.19. Run logs exist for lanes L1 and L5. One run ended 'FACTORY: BLOCKED BL-1289 back in Backlog behind BL-1290: the per-chunk allocations still left are in Curl.Console, which BL-1290 holds'. The final run ended 'FACTORY: DONE BL-1289'. The task was queued while its remaining work overlapped BL-1290's touches. That sent it back to Backlog repeatedly. This is a log-based reading only; the 6 claims were not traced one by one. Also in the same period, waitOverlapMinutes equals laneIdleMinutes (3847 of 3847): all lane idle time was overlap waiting.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-09-30 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object claims -ge 3 | Select-Object id,claims
```

- Expected: No task claimed 3 or more times.
- Actual: BL-1289 claims=6

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
