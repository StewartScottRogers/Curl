---
id: BL-1773
title: Fix AF-0095: BL-1488 cost 6.56 US dollars, 5.5 times the 1.19 median; lane 7's five Sonnet sub-agents used up the session budget
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-08
completed:
---
# BL-1773 — Fix AF-0095: BL-1488 cost 6.56 US dollars, 5.5 times the 1.19 median; lane 7's five Sonnet sub-agents used up the session budget

## Goal

The defect the audit office reported as AF-0095 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0095 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0095-bl-1488-cost-6-56-us-dollars-5-5-times-the-1-19-me.md`.

Location: `logs/BL-1488-20261007-111121-L7.jsonl`

Location: `logs/BL-1488-20261007-111121-L7.jsonl`

L7 run: 21.4 min, 184 tool calls, 5 Agent calls; $5.73, of which claude-sonnet-5-5 $4.86 and opus $0.87. It ended at 14:42:22 with stash and REQUEUE 'Session budget ran out with work in the~'. L2 then did the task in 4.8 min and 24 turns for $0.83 (DONE 14:52:01).

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Sort-Object costUsd -Descending | Select-Object -First 5 id,claims,costUsd,minutes,outcome
```

- Expected: No task over 3 times the median costUsd (3.57).
- Actual: BL-1488 costUsd 6.5614 (claims 2)

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
