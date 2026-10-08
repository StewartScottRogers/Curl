---
id: BL-1776
title: Fix AF-0098: BL-1585 cost 4.49 US dollars in one run, 3.8 times the median, with three Opus test-writer sub-agents
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-08
completed:
---
# BL-1776 — Fix AF-0098: BL-1585 cost 4.49 US dollars in one run, 3.8 times the median, with three Opus test-writer sub-agents

## Goal

The defect the audit office reported as AF-0098 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0098 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0098-bl-1585-cost-4-49-us-dollars-in-one-run-3-8-times.md`.

Location: `logs/BL-1585-20261007-111121-L2.jsonl`

Location: `logs/BL-1585-20261007-111121-L2.jsonl`

One run on L2: 17 min, 19 turns, 93 tool calls, 3 Agent calls (test-writer for 1, 4 and 5 runner test files at 13:36); all opus, $4.49.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1585' | Select-Object id,claims,costUsd,minutes
```

- Expected: costUsd at most 3.57.
- Actual: BL-1585 claims 1 costUsd 4.4928

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
