---
id: BL-1788
title: Fix AF-0110: BL-1587 cost 3.68 US dollars in one run, 3.1 times the median, with three Opus sub-agents
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-08
completed:
---
# BL-1788 — Fix AF-0110: BL-1587 cost 3.68 US dollars in one run, 3.1 times the median, with three Opus sub-agents

## Goal

The defect the audit office reported as AF-0110 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0110 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0110-bl-1587-cost-3-68-us-dollars-in-one-run-3-1-times.md`.

Location: `logs/BL-1587-20261007-111121-L8.jsonl`

Location: `logs/BL-1587-20261007-111121-L8.jsonl`

One run on L8: 8.3 min, 15 turns, 63 tool calls, 3 Agent calls; all opus, $3.68.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1587' | Select-Object id,claims,costUsd,minutes
```

- Expected: costUsd at most 3.57.
- Actual: BL-1587 claims 1 costUsd 3.6768

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
