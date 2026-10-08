---
id: BL-1778
title: Fix AF-0100: BL-1555 cost 4.33 US dollars in one run, 3.6 times the median, mostly in three Sonnet sub-agents
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-08
completed:
---
# BL-1778 — Fix AF-0100: BL-1555 cost 4.33 US dollars in one run, 3.6 times the median, mostly in three Sonnet sub-agents

## Goal

The defect the audit office reported as AF-0100 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0100 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0100-bl-1555-cost-4-33-us-dollars-in-one-run-3-6-times.md`.

Location: `logs/BL-1555-20261007-111121-L1.jsonl`

Location: `logs/BL-1555-20261007-111121-L1.jsonl`

One run on L1: 14.4 min, 28 turns, 104 tool calls, 3 Agent calls; claude-sonnet-5-5 $3.07 + opus $1.26.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1555' | Select-Object id,claims,costUsd,minutes
```

- Expected: costUsd at most 3.57.
- Actual: BL-1555 claims 1 costUsd 4.3333

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
