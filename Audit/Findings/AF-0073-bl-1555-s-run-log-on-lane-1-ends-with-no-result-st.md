---
id: AF-0073
title: BL-1555's run log on lane 1 ends with no result: still running (a Bash call 510 s in) when the logs were copied
auditor: process
severity: Low
status: proposed
reason:
key: process:logs:BL-1555:unfinished-run
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
# AF-0073 - BL-1555's run log on lane 1 ends with no result: still running (a Bash call 510 s in) when the logs were copied

## Summary

Low finding from the process auditor at `logs/BL-1555-20261007-111121-L1.jsonl`: BL-1555's run log on lane 1 ends with no result: still running (a Bash call 510 s in) when the logs were copied.

## Evidence

Location: `logs/BL-1555-20261007-111121-L1.jsonl`

unfinishedRuns lists BL-1555-20261007-111121-L1.jsonl; costUsd 0 after 11.08 min. Lane 1 claimed it 13:25:11; its last lane line is 13:33:05 'edit HandBuiltTlsProviderTests.Ech.cs'. The jsonl ends in a tool_progress heartbeat, elapsed_time_seconds 510. No timeout, usage limit or crash: the log copy at about 13:36 cut the run off mid-task.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).unfinishedRuns; Get-Content ..\logs\BL-1555-20261007-111121-L1.jsonl -Tail 1
```

- Expected: unfinishedRuns does not list BL-1555; its run ends with a result line.
- Actual: unfinishedRuns lists BL-1555-20261007-111121-L1.jsonl; its last line is a tool_progress heartbeat.

## Re-audits

## Log

- 2026-10-07: filed proposed.
