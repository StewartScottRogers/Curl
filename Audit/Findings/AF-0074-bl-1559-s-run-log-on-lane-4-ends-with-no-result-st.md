---
id: AF-0074
title: BL-1559's run log on lane 4 ends with no result: still running when the logs were copied
auditor: process
severity: Low
status: proposed
reason:
key: process:logs:BL-1559:unfinished-run
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
# AF-0074 - BL-1559's run log on lane 4 ends with no result: still running when the logs were copied

## Summary

Low finding from the process auditor at `logs/BL-1559-20261007-111121-L4.jsonl`: BL-1559's run log on lane 4 ends with no result: still running when the logs were copied.

## Evidence

Location: `logs/BL-1559-20261007-111121-L4.jsonl`

unfinishedRuns lists BL-1559-20261007-111121-L4.jsonl; costUsd 0, 5.12 min. Claimed 13:31:09; last lane line 13:35:33 'build ok'. The jsonl ends in a Bash tool_progress heartbeat at 30 s. Stopped by the log copy, not by a timeout, a limit or a crash.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).unfinishedRuns; Get-Content ..\logs\BL-1559-20261007-111121-L4.jsonl -Tail 1
```

- Expected: unfinishedRuns does not list BL-1559.
- Actual: unfinishedRuns lists BL-1559-20261007-111121-L4.jsonl; its last line is a tool_progress heartbeat.

## Re-audits

## Log

- 2026-10-07: filed proposed.
