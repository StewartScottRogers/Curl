---
id: BL-1692
title: Fix AF-0073: BL-1555's run log on lane 1 ends with no result: still running (a Bash call 510 s in) when the logs were copied
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-08
completed: 2026-10-07
---
# BL-1692 — Fix AF-0073: BL-1555's run log on lane 1 ends with no result: still running (a Bash call 510 s in) when the logs were copied

## Goal

The defect the audit office reported as AF-0073 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0073 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0073-bl-1555-s-run-log-on-lane-1-ends-with-no-result-st.md`.

Location: `logs/BL-1555-20261007-111121-L1.jsonl`

Location: `logs/BL-1555-20261007-111121-L1.jsonl`

unfinishedRuns lists BL-1555-20261007-111121-L1.jsonl; costUsd 0 after 11.08 min. Lane 1 claimed it 13:25:11; its last lane line is 13:33:05 'edit HandBuiltTlsProviderTests.Ech.cs'. The jsonl ends in a tool_progress heartbeat, elapsed_time_seconds 510. No timeout, usage limit or crash: the log copy at about 13:36 cut the run off mid-task.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).unfinishedRuns; Get-Content ..\logs\BL-1555-20261007-111121-L1.jsonl -Tail 1
```

- Expected: unfinishedRuns does not list BL-1555; its run ends with a result line.
- Actual: unfinishedRuns lists BL-1555-20261007-111121-L1.jsonl; its last line is a tool_progress heartbeat.

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-07 (lane 8): The run was never unfinished. The live log
  `Z:\repos\Curl.logs\BL-1555-20261007-111121-L1.jsonl` (1,397,043 bytes, last written 13:39)
  now ends with a `"type":"result"` line, `duration_ms` 866526, closing
  `FACTORY: DONE BL-1555 ...`. The audit's copy at about 13:36 caught the run 11 minutes in,
  three minutes before it finished. A copy taken now ends with that result line, so the
  reproduction gives the expected result: BL-1555's run ends with a result line, and
  unfinishedRuns has no unfinished run to list. A lane may not run
  `Audit/Tools/Measure-FactoryProcess.ps1` (the guard hook refuses audit paths), so this
  check read the log's tail directly. The process auditor's re-audit will confirm it.
- No change to `RunDarkFactory.ps1`: the factory wrote the log correctly. What went wrong is
  that an audit run alongside a shift (`-AlongsideShift`) copies logs that are still being
  written. A fix there, such as dropping a log whose run is still in progress or whose last
  line is a `tool_progress` heartbeat younger than the copy, belongs to the audit tooling.
  That is an audit path, and only an interactive session can work it; the board refuses a
  lane filing that task, so none was filed.
- Build clean; fast tests: 0 failed across the solution.

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. BL-1555's log ends with its result line; the audit's copy cut it off mid-run, so it was never unfinished
