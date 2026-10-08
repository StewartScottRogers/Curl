---
id: BL-1783
title: Fix AF-0105: BL-1499 cost 3.86 US dollars in one run, 3.2 times the median, with about 97 test-run mentions
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-08
completed:
---
# BL-1783 — Fix AF-0105: BL-1499 cost 3.86 US dollars in one run, 3.2 times the median, with about 97 test-run mentions

## Goal

The defect the audit office reported as AF-0105 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0105 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0105-bl-1499-cost-3-86-us-dollars-in-one-run-3-2-times.md`.

Location: `logs/BL-1499-20261007-111121-L4.jsonl`

Location: `logs/BL-1499-20261007-111121-L4.jsonl`

One run on L4: 17 min of model time (36.7 min from claim to Done), 57 turns, 55 tool calls, 43 Bash, 97 'dotnet test' mentions in the run log; all opus, $3.86.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1499' | Select-Object id,claims,costUsd,minutes
```

- Expected: costUsd at most 3.57.
- Actual: BL-1499 claims 1 costUsd 3.8567

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
