---
id: BL-1696
title: Fix AF-0077: BL-1623's run log on lane 6 ends with no result: still writing tests when the logs were copied
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-08
completed:
---
# BL-1696 — Fix AF-0077: BL-1623's run log on lane 6 ends with no result: still writing tests when the logs were copied

## Goal

The defect the audit office reported as AF-0077 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0077 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0077-bl-1623-s-run-log-on-lane-6-ends-with-no-result-st.md`.

Location: `logs/BL-1623-20261007-111121-L6.jsonl`

Location: `logs/BL-1623-20261007-111121-L6.jsonl`

unfinishedRuns lists BL-1623-20261007-111121-L6.jsonl; costUsd 0, 3.3 min. Lane 6 was re-added 13:30:38 and claimed it 13:32:58; last lane line 13:36:13 'write PlatformSshAgentConnectorTests.cs'. The jsonl ends in that Write's tool result. Cut off by the log copy.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).unfinishedRuns; Select-String -Path ..\logs\DarkFactory-20261007-111121-L6.log -Pattern 'BL-1623'
```

- Expected: unfinishedRuns does not list BL-1623.
- Actual: unfinishedRuns lists BL-1623-20261007-111121-L6.jsonl.

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
