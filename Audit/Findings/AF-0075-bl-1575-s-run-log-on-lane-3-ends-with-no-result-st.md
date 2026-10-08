---
id: AF-0075
title: BL-1575's run log on lane 3 ends with no result: still running (an Agent call) when the logs were copied
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-10-08_0748.md: the reproduction no longer reproduces.
key: process:logs:BL-1575:unfinished-run
reproduction: none
task: BL-1694
tasks: BL-1694
found: 2026-10-07
found-at: 0fcb5afc262ef32bb48ad058cf1f4a2b2c68d511
scorecard: 2026-10-07_1336.md
duplicate-of:
closed: 2026-10-08
closed-how: reliable-reaudit
closed-by: 2026-10-08_0748.md
---
# AF-0075 - BL-1575's run log on lane 3 ends with no result: still running (an Agent call) when the logs were copied

## Summary

Low finding from the process auditor at `logs/BL-1575-20261007-111121-L3.jsonl`: BL-1575's run log on lane 3 ends with no result: still running (an Agent call) when the logs were copied.

## Evidence

Location: `logs/BL-1575-20261007-111121-L3.jsonl`

unfinishedRuns lists BL-1575-20261007-111121-L3.jsonl; costUsd 0, 3.48 min. Claimed 13:32:47; last lane line 13:36:02 'edit LibcurlSourceCodeTests.cs'. The jsonl ends in an Agent tool_progress heartbeat at 120 s. Cut off by the log copy.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).unfinishedRuns; Get-Content ..\logs\BL-1575-20261007-111121-L3.jsonl -Tail 1
```

- Expected: unfinishedRuns does not list BL-1575.
- Actual: unfinishedRuns lists BL-1575-20261007-111121-L3.jsonl; its last line is a tool_progress heartbeat.

## Re-audits

- 2026-10-08 | 2026-10-08_0748.md | reproduces: no | Ran the reproduction: unfinishedRuns is empty. The tail of BL-1575-20261007-111121-L3.jsonl is type result, subtype success, ending 'FACTORY: DONE BL-1575 every test in DefaultConfigFileSearchTests to LibcurlSourceCodeTransferFileTests ...'.

## Log

- 2026-10-07: filed proposed.
- 2026-10-07: proposed -> accepted.
- 2026-10-08: accepted -> closed. Re-audit 2026-10-08_0748.md: the reproduction no longer reproduces.
