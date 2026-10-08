---
id: BL-1775
title: Fix AF-0097: BL-1486 cost 4.65 US dollars in one run, 3.9 times the median, mostly in five Sonnet sub-agents
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-08
completed:
---
# BL-1775 — Fix AF-0097: BL-1486 cost 4.65 US dollars in one run, 3.9 times the median, mostly in five Sonnet sub-agents

## Goal

The defect the audit office reported as AF-0097 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0097 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0097-bl-1486-cost-4-65-us-dollars-in-one-run-3-9-times.md`.

Location: `logs/BL-1486-20261007-111121-L7.jsonl`

Location: `logs/BL-1486-20261007-111121-L7.jsonl`

One run on L7: 15.7 min, 38 turns, 125 tool calls (66 Bash, 24 Read), 5 Agent calls; claude-sonnet-5-5 $2.97 + opus $1.68.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1486' | Select-Object id,claims,costUsd,minutes
```

- Expected: costUsd at most 3.57.
- Actual: BL-1486 claims 1 costUsd 4.6491

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
