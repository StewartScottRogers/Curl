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
completed:
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

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
