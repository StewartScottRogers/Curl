---
id: AF-0192
title: BL-2127's run log ends mid-Bash with no result because the audit copied the logs 2.3 minutes into a live run
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:BL-2127:unfinished-run
reproduction: none
task: none
tasks:
found: 2026-10-13
found-at: c86d04eeb8477108eb6d9195bb036a4ddaecc5fc
scorecard: 2026-10-13_0925.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0192 - BL-2127's run log ends mid-Bash with no result because the audit copied the logs 2.3 minutes into a live run

## Summary

Low finding from the process auditor at `logs/BL-2127-20261013-092151-L1.jsonl:73`: BL-2127's run log ends mid-Bash with no result because the audit copied the logs 2.3 minutes into a live run.

## Evidence

Location: `logs/BL-2127-20261013-092151-L1.jsonl:73`

Measure-FactoryProcess.ps1 unfinishedRuns lists BL-2127-20261013-092151-L1.jsonl. The trace has 73 lines, with timestamps from 2026-10-13T16:22:28Z to 16:24:41Z, and no "type":"result" line. The last line is a task_started for a Bash 'dotnet test ... Show failing test details', after lane log line 5 '09:24:36 L1 BL-2127 test 26591/26599 pass'. Shift 20261013-092151 started at 09:21:52, ci-runs.json was saved at 09:25, and the audit is 20261013-092506. The run was not stopped: it was still working, and the audit took its copy alongside a live shift. Nothing was lost, but the window's numbers count BL-2127 as open, with costUsd 0. The task is in Tasks/Doing at the audited commit.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-12 -LogRoot Z:\repos\Curl.audit\20261013-092506\logs -CiRunsJson Z:\repos\Curl.audit\20261013-092506\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).unfinishedRuns; Select-String -Path Z:\repos\Curl.audit\20261013-092506\logs\BL-2127-20261013-092151-L1.jsonl -Pattern '"type":"result"' | Measure-Object
```

- Expected: unfinishedRuns empty; every run log ends with a result line
- Actual: unfinishedRuns includes BL-2127-20261013-092151-L1.jsonl; 0 result lines

## Re-audits

## Log

- 2026-10-13: filed proposed.
- 2026-10-13: proposed -> accepted.
