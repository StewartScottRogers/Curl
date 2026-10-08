---
id: BL-1777
title: Fix AF-0099: BL-1683 cost 4.42 US dollars, 3.7 times the median, over two runs; the first was stashed and requeued for a held project
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-08
completed:
---
# BL-1777 — Fix AF-0099: BL-1683 cost 4.42 US dollars, 3.7 times the median, over two runs; the first was stashed and requeued for a held project

## Goal

The defect the audit office reported as AF-0099 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0099 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0099-bl-1683-cost-4-42-us-dollars-3-7-times-the-median.md`.

Location: `logs/DarkFactory-20261007-111121-L5.log`

Location: `logs/DarkFactory-20261007-111121-L5.log`

L5 claimed it at 17:53:30, ran 21.9 min and 56 turns ($2.46), then stash and REQUEUE at 18:46:58: 'Needs Curl.Console.UnitTests, held by B~'. L1 in shift 201432 claimed it at 20:14:56 and was Done at 20:36:00 after 53 turns ($1.96).

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1683' | Select-Object id,claims,costUsd,minutes
```

- Expected: costUsd at most 3.57.
- Actual: BL-1683 claims 2 costUsd 4.4205

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
