---
id: AF-0077
title: BL-1623's run log on lane 6 ends with no result: still writing tests when the logs were copied
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:BL-1623:unfinished-run
reproduction: none
task: BL-1696
tasks: BL-1696
found: 2026-10-07
found-at: 0fcb5afc262ef32bb48ad058cf1f4a2b2c68d511
scorecard: 2026-10-07_1336.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0077 - BL-1623's run log on lane 6 ends with no result: still writing tests when the logs were copied

## Summary

Low finding from the process auditor at `logs/BL-1623-20261007-111121-L6.jsonl`: BL-1623's run log on lane 6 ends with no result: still writing tests when the logs were copied.

## Evidence

Location: `logs/BL-1623-20261007-111121-L6.jsonl`

unfinishedRuns lists BL-1623-20261007-111121-L6.jsonl; costUsd 0, 3.3 min. Lane 6 was re-added 13:30:38 and claimed it 13:32:58; last lane line 13:36:13 'write PlatformSshAgentConnectorTests.cs'. The jsonl ends in that Write's tool result. Cut off by the log copy.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).unfinishedRuns; Select-String -Path ..\logs\DarkFactory-20261007-111121-L6.log -Pattern 'BL-1623'
```

- Expected: unfinishedRuns does not list BL-1623.
- Actual: unfinishedRuns lists BL-1623-20261007-111121-L6.jsonl.

## Re-audits

## Log

- 2026-10-07: filed proposed.
- 2026-10-07: proposed -> accepted.
