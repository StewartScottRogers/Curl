---
id: BL-1789
title: Fix AF-0111: BL-1557 cost 3.63 US dollars in one 118-turn run, 3.05 times the median
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-08
completed:
---
# BL-1789 — Fix AF-0111: BL-1557 cost 3.63 US dollars in one 118-turn run, 3.05 times the median

## Goal

The defect the audit office reported as AF-0111 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0111 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0111-bl-1557-cost-3-63-us-dollars-in-one-118-turn-run-3.md`.

Location: `logs/BL-1557-20261007-111121-L1.jsonl`

Location: `logs/BL-1557-20261007-111121-L1.jsonl`

One run on L1: 22.1 min, 118 turns (the most of any outlier), 117 tool calls, 0 Read; all opus, $3.63.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1557' | Select-Object id,claims,costUsd,minutes
```

- Expected: costUsd at most 3.57.
- Actual: BL-1557 claims 1 costUsd 3.6288

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
