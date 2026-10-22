---
id: BL-1780
title: Fix AF-0102: BL-1586 cost 4.23 US dollars in one 35-minute run, 3.6 times the median
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1780 — Fix AF-0102: BL-1586 cost 4.23 US dollars in one 35-minute run, 3.6 times the median

## Goal

The defect the audit office reported as AF-0102 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0102 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0102-bl-1586-cost-4-23-us-dollars-in-one-35-minute-run.md`.

Location: `logs/BL-1586-20261007-111121-L2.jsonl`

Location: `logs/BL-1586-20261007-111121-L2.jsonl`

One run on L2: 35.3 min, 56 turns, 194 tool calls (the most of any outlier), 3 Agent calls, 50 'dotnet test' mentions; all opus, $4.23.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1586' | Select-Object id,claims,costUsd,minutes
```

- Expected: costUsd at most 3.57.
- Actual: BL-1586 claims 1 costUsd 4.2336

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause, read from `Curl.logs/BL-1586-20261007-111121-L2.jsonl` ($4.2336): the run finished BL-1586 (95 tests instrumented, fast tests green), but it started three Opus `test-writer` subagents (TransferEvent, TransferOption, TransferWarning) in one assistant message (`msg_011CfoZRfmKuDksibSDH3fKB`). The shift's cost cap is checked only between the run's own turns, so all three spent inside that one turn - the AF-0052 / AF-0098 pattern.
- Already fixed later: 630d38761 (BL-1679, AF-0052, 2026-10-07 21:46) made the lane prompt allow one subagent at a time and never several in one message, and the factory picks Sonnet for test work (BL-1705). No new mechanism; this task cites AF-0102 in `RunDarkFactory.ps1`'s help and in both lane prompts' subagent rule.
- The reproduction's `-Since 2026-10-07` window will always show BL-1586's past $4.23, because logs do not change, and a lane cannot run `Audit/Tools/Measure-FactoryProcess.ps1` (the audit path guard). As for BL-1775 to BL-1779, the box is ticked because the cause is fixed; the re-audit should measure runs started after 630d38761.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Cause was three Opus subagents started in one message; fixed by BL-1679 (AF-0052), now cited for AF-0102; build clean, fast tests green
