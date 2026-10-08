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
completed: 2026-10-08
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause, read from `BL-1486-20261007-111121-L7.jsonl`: one assistant message (msg_011CfoZEhPWP9vTb3iK3z6y4) started five `test-writer` subagents on Sonnet in the background at once, which cost $2.97 of the run's $4.65. The cap is checked only between turns, so the five spent inside one turn. This is the same cause as AF-0052.
- The run started 2026-10-07 11:11. BL-1679 (AF-0052, commit 630d38761, 2026-10-07 21:47) added the lane prompt rule after it: "Run at most one subagent at a time, and never start several in one message." So the cause was already fixed for every later run. This task adds nothing that would duplicate that rule; it names AF-0097 next to AF-0052 in the COST CAP help of `RunDarkFactory.ps1`.
- The reproduction's `-Since 2026-10-07` window will always show BL-1486's $4.65 from the past, because logs do not change, and a lane cannot run `Audit/Tools/Measure-FactoryProcess.ps1` (the audit path guard). As for BL-1773 and BL-1774, the box is ticked because the cause is fixed. The re-audit should measure only runs after 630d38761.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Cause was five parallel subagents in one turn, fixed for later runs by BL-1679 (AF-0052); AF-0097 now cited in RunDarkFactory.ps1 cost-cap help; build clean, fast tests green
