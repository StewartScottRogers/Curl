---
id: BL-1786
title: Fix AF-0108: BL-1645 cost 3.75 US dollars in one 28-minute run, 3.1 times the median
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-08
completed:
---
# BL-1786 — Fix AF-0108: BL-1645 cost 3.75 US dollars in one 28-minute run, 3.1 times the median

## Goal

The defect the audit office reported as AF-0108 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0108 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0108-bl-1645-cost-3-75-us-dollars-in-one-28-minute-run.md`.

Location: `logs/BL-1645-20261007-111121-L4.jsonl`

Location: `logs/BL-1645-20261007-111121-L4.jsonl`

One run on L4: 27.7 min, 51 turns, 45 Bash calls, 41 'dotnet test' mentions; all opus, $3.75. This is the follow-on to BL-1525's 5x curve speed target.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1645' | Select-Object id,claims,costUsd,minutes
```

- Expected: costUsd at most 3.57.
- Actual: BL-1645 claims 1 costUsd 3.7467

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
