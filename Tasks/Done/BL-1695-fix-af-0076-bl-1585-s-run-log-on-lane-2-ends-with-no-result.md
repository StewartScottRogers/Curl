---
id: BL-1695
title: Fix AF-0076: BL-1585's run log on lane 2 ends with no result: claimed 84 seconds before the logs were copied
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-08
completed: 2026-10-07
---
# BL-1695 — Fix AF-0076: BL-1585's run log on lane 2 ends with no result: claimed 84 seconds before the logs were copied

## Goal

The defect the audit office reported as AF-0076 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0076 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0076-bl-1585-s-run-log-on-lane-2-ends-with-no-result-cl.md`.

Location: `logs/BL-1585-20261007-111121-L2.jsonl`

Location: `logs/BL-1585-20261007-111121-L2.jsonl`

unfinishedRuns lists BL-1585-20261007-111121-L2.jsonl; costUsd 0, 1.4 min. Claimed 13:34:52 (audited commit 0fcb5afc2 is 'claim BL-1585 on dark factory lane 2'); last lane line 13:36:16 'agent test-writer'. The jsonl ends in a task_notification. Still running at the copy.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).unfinishedRuns; Select-String -Path ..\logs\DarkFactory-20261007-111121-L2.log -Pattern 'BL-1585'
```

- Expected: unfinishedRuns does not list BL-1585.
- Actual: unfinishedRuns lists BL-1585-20261007-111121-L2.jsonl; L2 shows only claim 13:34:52 and agent 13:36:16.

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. AF-0076 no longer reproduces: BL-1585's L2 run log ends with its result line; the audit had copied it mid-run
