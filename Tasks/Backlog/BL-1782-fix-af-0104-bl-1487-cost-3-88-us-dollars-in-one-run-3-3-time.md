---
id: BL-1782
title: Fix AF-0104: BL-1487 cost 3.88 US dollars in one run, 3.3 times the median, mostly in four Sonnet sub-agents
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-08
completed:
---
# BL-1782 — Fix AF-0104: BL-1487 cost 3.88 US dollars in one run, 3.3 times the median, mostly in four Sonnet sub-agents

## Goal

The defect the audit office reported as AF-0104 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0104 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0104-bl-1487-cost-3-88-us-dollars-in-one-run-3-3-times.md`.

Location: `logs/BL-1487-20261007-111121-L6.jsonl`

Location: `logs/BL-1487-20261007-111121-L6.jsonl`

One run on L6: 17.8 min, 27 turns, 98 tool calls, 4 Agent calls, 9 'has been denied' messages; claude-sonnet-5-5 $2.58 + opus $1.30.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1487' | Select-Object id,claims,costUsd,minutes
```

- Expected: costUsd at most 3.57.
- Actual: BL-1487 claims 1 costUsd 3.8816

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
