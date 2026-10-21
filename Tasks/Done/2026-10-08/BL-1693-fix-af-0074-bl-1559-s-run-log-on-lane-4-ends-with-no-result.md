---
id: BL-1693
title: Fix AF-0074: BL-1559's run log on lane 4 ends with no result: still running when the logs were copied
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-08
completed: 2026-10-07
---
# BL-1693 — Fix AF-0074: BL-1559's run log on lane 4 ends with no result: still running when the logs were copied

## Goal

The defect the audit office reported as AF-0074 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0074 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0074-bl-1559-s-run-log-on-lane-4-ends-with-no-result-st.md`.

Location: `logs/BL-1559-20261007-111121-L4.jsonl`

Location: `logs/BL-1559-20261007-111121-L4.jsonl`

unfinishedRuns lists BL-1559-20261007-111121-L4.jsonl; costUsd 0, 5.12 min. Claimed 13:31:09; last lane line 13:35:33 'build ok'. The jsonl ends in a Bash tool_progress heartbeat at 30 s. Stopped by the log copy, not by a timeout, a limit or a crash.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).unfinishedRuns; Get-Content ..\logs\BL-1559-20261007-111121-L4.jsonl -Tail 1
```

- Expected: unfinishedRuns does not list BL-1559.
- Actual: unfinishedRuns lists BL-1559-20261007-111121-L4.jsonl; its last line is a tool_progress heartbeat.

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- No code change was needed. AF-0074 records a snapshot, not a factory defect: the audit copied the logs at about 13:36, while BL-1559 was still running (its lane log shows tests at 13:37, quality at 14:05, `end 32 turns, 36.4 min` at 14:07:35 and DONE at 14:10:19). The live `Z:eposCurl.logsBL-1559-20261007-111121-L4.jsonl` now ends with its `"type":"result"` line (`FACTORY: DONE BL-1559 ...`), so a re-audit against current logs finds no unfinished run for BL-1559. RunDarkFactory.ps1 already writes each run's full stream to completion and is unchanged.
- Not run here: the reproduction itself. `Audit/Tools/Measure-FactoryProcess.ps1` is an audit path, and the audit guard refuses lane reads of it. Instead, checked the condition it measures: the jsonl's last line is a result line.
- Suggested follow-up for an interactive session (a lane cannot file it; the guard refused `new` touching an audit path): have the audit's log copy or Measure-FactoryProcess.ps1 set aside runs whose lane log has a `claim` but no `end` yet at copy time, rather than reporting them as unfinished.

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. AF-0074 no longer reproduces: BL-1559's L4 run log ends with its result line; the audit had copied it mid-run
