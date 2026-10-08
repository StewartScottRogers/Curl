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
completed: 2026-10-08
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause, read from `Curl.logs/BL-1555-20261007-111121-L1.jsonl`: the run started three Sonnet `test-writer` subagents ("Diagnostics main TLS test file", "Diagnostics Curves/Ech/ClientHello", "Diagnostics remaining TLS files"), each in its own message but with `run_in_background: true`, so all three ran at once and spent inside the turns between cap checks: Sonnet $3.07 of the run's $4.33.
- The run (2026-10-07 11:11) predates 630d38761 (BL-1679, AF-0052, 2026-10-07 21:47), which tells every run to keep to one subagent at a time. Its wording, "never start several in one message", did not name this case - one per message, each in the background - so this task adds it to both prompt copies ("or start one in the background while another runs", citing AF-0100) and names AF-0100 beside AF-0097 and AF-0098 in the COST CAP help. No new mechanism: the BL-1679 rule plus the cap already bound later runs.
- The reproduction's `-Since 2026-10-07` window will always show BL-1555's past $4.33, because logs do not change, and a lane cannot run `Audit/Tools/Measure-FactoryProcess.ps1` (the audit path guard). As for BL-1775 to BL-1777, the box is ticked because the cause is fixed; the re-audit should measure runs started after 630d38761 and this change.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Cause was three Sonnet test-writers run in the background at once, before the BL-1679 one-subagent rule; prompt now also forbids a background subagent beside another (AF-0100); build clean, fast tests green
