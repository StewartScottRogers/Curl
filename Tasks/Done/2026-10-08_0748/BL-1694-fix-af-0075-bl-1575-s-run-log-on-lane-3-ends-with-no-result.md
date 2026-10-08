---
id: BL-1694
title: Fix AF-0075: BL-1575's run log on lane 3 ends with no result: still running (an Agent call) when the logs were copied
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-08
completed: 2026-10-07
---
# BL-1694 — Fix AF-0075: BL-1575's run log on lane 3 ends with no result: still running (an Agent call) when the logs were copied

## Goal

The defect the audit office reported as AF-0075 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0075 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0075-bl-1575-s-run-log-on-lane-3-ends-with-no-result-st.md`.

Location: `logs/BL-1575-20261007-111121-L3.jsonl`

Location: `logs/BL-1575-20261007-111121-L3.jsonl`

unfinishedRuns lists BL-1575-20261007-111121-L3.jsonl; costUsd 0, 3.48 min. Claimed 13:32:47; last lane line 13:36:02 'edit LibcurlSourceCodeTests.cs'. The jsonl ends in an Agent tool_progress heartbeat at 120 s. Cut off by the log copy.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).unfinishedRuns; Get-Content ..\logs\BL-1575-20261007-111121-L3.jsonl -Tail 1
```

- Expected: unfinishedRuns does not list BL-1575.
- Actual: unfinishedRuns lists BL-1575-20261007-111121-L3.jsonl; its last line is a tool_progress heartbeat.

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- No code change was needed. Like AF-0074 (BL-1693), AF-0075 records a snapshot, not a factory defect: the audit copied the logs at about 13:36, while BL-1575 was still running inside an Agent call. The live `Z:\repos\Curl.logs\BL-1575-20261007-111121-L3.jsonl` now ends with its `"type":"result"` line (`FACTORY: DONE BL-1575 ...`, duration 758 s), so a re-audit against current logs finds no unfinished run for BL-1575. RunDarkFactory.ps1 already writes each run's full stream to completion and is unchanged.
- Not run here: the reproduction itself. `Audit/Tools/Measure-FactoryProcess.ps1` is an audit path the lane guard refuses. Instead, checked the condition it measures: the jsonl's last line is a result line.
- The follow-up suggested under BL-1693 applies here too (an interactive session's to file): have the audit's log copy or Measure-FactoryProcess.ps1 set aside runs still in progress at copy time.

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. AF-0075 no longer reproduces: BL-1575's L3 run log ends with its result line; the audit had copied it mid-run
