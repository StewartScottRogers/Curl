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
completed:
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

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
