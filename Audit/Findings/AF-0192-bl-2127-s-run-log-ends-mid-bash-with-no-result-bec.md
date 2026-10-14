---
id: AF-0192
title: BL-2127's run log ends mid-Bash with no result because the audit copied the logs 2.3 minutes into a live run
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-10-13_1822.md: the reproduction no longer reproduces.
key: process:logs:BL-2127:unfinished-run
reproduction: none
task: BL-2151
tasks: BL-2151
found: 2026-10-13
found-at: c86d04eeb8477108eb6d9195bb036a4ddaecc5fc
scorecard: 2026-10-13_0925.md
duplicate-of:
closed: 2026-10-13
closed-how: reliable-reaudit
closed-by: 2026-10-13_1822.md
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

- 2026-10-13 | 2026-10-13_1822.md | reproduces: no | The given reproduction still lists BL-2127-20261013-092151-L1.jsonl in unfinishedRuns, with 0 result lines in the old copy. This window's copy of the same file has 1 '"type":"result"' line. Re-audited by mechanism over this window (shifts finished 28 tasks): unfinishedRuns is empty. The log copy ended at 18:21:16, before lane 1's next claim (BL-2160 at 18:22:23), so no run log was cut off mid-run.

## Log

- 2026-10-13: filed proposed.
- 2026-10-13: proposed -> accepted.
- 2026-10-13: accepted -> closed. Re-audit 2026-10-13_1822.md: the reproduction no longer reproduces.
