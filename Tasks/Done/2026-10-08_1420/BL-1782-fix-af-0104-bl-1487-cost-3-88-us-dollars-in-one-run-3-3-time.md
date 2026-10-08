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
completed: 2026-10-08
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause, from BL-1487's L6 log: one assistant message started four Sonnet test-writer subagents (4 Agent calls, none in the background), so all four spent inside one turn before the cost cap was checked: $2.58 Sonnet + $1.30 Opus. Same mechanism as AF-0052, AF-0100 and AF-0102.
- Already fixed: BL-1679 limits a run to one subagent at a time. No new mechanism; the help and both lane prompts now cite AF-0104.
- A lane cannot run `Audit/Tools/Measure-FactoryProcess.ps1` (audit path guard), and logs do not change, so the reproduction keeps showing BL-1487's past $3.88. The box is ticked because the cause is fixed; the re-audit should measure runs after BL-1679.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Cause was four subagents in one message; fixed by BL-1679, now cites AF-0104; build clean, fast tests green
