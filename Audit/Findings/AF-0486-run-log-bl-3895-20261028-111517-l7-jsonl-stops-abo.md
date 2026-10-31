---
id: AF-0486
title: Run log BL-3895-20261028-111517-L7.jsonl stops about 2 minutes before its run ended, losing the result record and its cost
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:BL-3895:unfinished-run
reproduction: none
task: BL-4239
tasks: BL-4239
found: 2026-10-29
found-at: ce4fb84772365b99da350fd339cc0c2e9cb2ce30
scorecard: 2026-10-29_0606.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0486 - Run log BL-3895-20261028-111517-L7.jsonl stops about 2 minutes before its run ended, losing the result record and its cost

## Summary

Low finding from the process auditor at `logs/BL-3895-20261028-111517-L7.jsonl`: Run log BL-3895-20261028-111517-L7.jsonl stops about 2 minutes before its run ended, losing the result record and its cost.

## Evidence

Location: `logs/BL-3895-20261028-111517-L7.jsonl`

Measure-FactoryProcess lists this file in unfinishedRuns. Its last event is at 2026-10-28T21:01:04Z (14:01:04 Arizona); the file was last modified at 14:01, and its last tool call was a dotnet test with a temporary 'false &&' mutation in CurlCommandRunner.cs. The lane log shows the run carried on: edits at 14:01:34, test 3/3 pass at 14:01:59, move Done at 14:02:37, commit at 14:02:45, 'end 60 turns, 16 min' at 14:02:55, integrated at 14:06:53 (DarkFactory-20261028-111517-L7.log:76-87). The run was recovered and the task finished, but the stream log lost its tail and result line. That run's tokens and cost are missing from costUsd.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).unfinishedRuns; Select-String -Path Z:/repos/Curl.audit/20261029-060658/logs/BL-3895-20261028-111517-L7.jsonl -Pattern '"type":"result"'
```

- Expected: unfinishedRuns empty; the run log ends with a result line.
- Actual: unfinishedRuns includes BL-3895-20261028-111517-L7.jsonl; no result line in it.

## Re-audits

- 2026-10-31 | 2026-10-31_0358.md | not re-audited | overlaps planted defect PD-503 in BL-4999-20261030-235500-L9.jsonl, so the auditor's verdict (reproduces yes) is set aside: unfinishedRuns = ['BL-4999-20261030-235500-L9.jsonl']. The run log stops after three assistant lines ('Running the fast tests.') with no result record, so its cost is lost. Select-String '"type":"result"' finds no match. No lane trace or coordinator line names BL-4999.
- 2026-10-31 | 2026-10-31_1149.md | not re-audited | overlaps planted defect PD-503 in BL-4999-20261031-090000-L9.jsonl, so the auditor's verdict (reproduces no) is set aside: By mechanism over this window: unfinishedRuns lists one file, BL-4999-20261031-090000-L9.jsonl, but its cause is different. No lane claimed BL-4999 and there was no lane 9 (filed as process:logs:BL-4999:unfinished-run). All 30 run logs written by real lane claims hold their type=result record, so no run log stopped before its run ended.

## Log

- 2026-10-29: filed proposed.
- 2026-10-29: proposed -> accepted.
