---
id: AF-0076
title: BL-1585's run log on lane 2 ends with no result: claimed 84 seconds before the logs were copied
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:BL-1585:unfinished-run
reproduction: none
task: none
tasks:
found: 2026-10-07
found-at: 0fcb5afc262ef32bb48ad058cf1f4a2b2c68d511
scorecard: 2026-10-07_1336.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0076 - BL-1585's run log on lane 2 ends with no result: claimed 84 seconds before the logs were copied

## Summary

Low finding from the process auditor at `logs/BL-1585-20261007-111121-L2.jsonl`: BL-1585's run log on lane 2 ends with no result: claimed 84 seconds before the logs were copied.

## Evidence

Location: `logs/BL-1585-20261007-111121-L2.jsonl`

unfinishedRuns lists BL-1585-20261007-111121-L2.jsonl; costUsd 0, 1.4 min. Claimed 13:34:52 (audited commit 0fcb5afc2 is 'claim BL-1585 on dark factory lane 2'); last lane line 13:36:16 'agent test-writer'. The jsonl ends in a task_notification. Still running at the copy.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).unfinishedRuns; Select-String -Path ..\logs\DarkFactory-20261007-111121-L2.log -Pattern 'BL-1585'
```

- Expected: unfinishedRuns does not list BL-1585.
- Actual: unfinishedRuns lists BL-1585-20261007-111121-L2.jsonl; L2 shows only claim 13:34:52 and agent 13:36:16.

## Re-audits

## Log

- 2026-10-07: filed proposed.
- 2026-10-07: proposed -> accepted.
