---
id: BL-2151
title: Fix AF-0192: BL-2127's run log ends mid-Bash with no result because the audit copied the logs 2.3 minutes into a live run
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-10-13
completed:
---
# BL-2151 — Fix AF-0192: BL-2127's run log ends mid-Bash with no result because the audit copied the logs 2.3 minutes into a live run

## Goal

The defect the audit office reported as AF-0192 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0192 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0192-bl-2127-s-run-log-ends-mid-bash-with-no-result-bec.md`.

Location: `logs/BL-2127-20261013-092151-L1.jsonl:73`

Location: `logs/BL-2127-20261013-092151-L1.jsonl:73`

Measure-FactoryProcess.ps1 unfinishedRuns lists BL-2127-20261013-092151-L1.jsonl. The trace has 73 lines, with timestamps from 2026-10-13T16:22:28Z to 16:24:41Z, and no "type":"result" line. The last line is a task_started for a Bash 'dotnet test ... Show failing test details', after lane log line 5 '09:24:36 L1 BL-2127 test 26591/26599 pass'. Shift 20261013-092151 started at 09:21:52, ci-runs.json was saved at 09:25, and the audit is 20261013-092506. The run was not stopped: it was still working, and the audit took its copy alongside a live shift. Nothing was lost, but the window's numbers count BL-2127 as open, with costUsd 0. The task is in Tasks/Doing at the audited commit.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-12 -LogRoot Z:\repos\Curl.audit\20261013-092506\logs -CiRunsJson Z:\repos\Curl.audit\20261013-092506\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).unfinishedRuns; Select-String -Path Z:\repos\Curl.audit\20261013-092506\logs\BL-2127-20261013-092151-L1.jsonl -Pattern '"type":"result"' | Measure-Object
```

- Expected: unfinishedRuns empty; every run log ends with a result line
- Actual: unfinishedRuns includes BL-2127-20261013-092151-L1.jsonl; 0 result lines

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Interactive only. The log copy and the unfinishedRuns count belong to the audit office tooling, which a lane may not read or change. RunDarkFactory.ps1 writes each run log correctly and cannot know a copy was taken mid-run. Fix: the audit skips or marks logs of runs still live (newest write within a few minutes, or task still in Doing) when copying alongside a shift. Remove lane: no once an interactive session has confirmed that audit-side change is wanted and works on the audit branch.

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Backlog. Interactive only: the fix is in the audit office tooling, which lanes cannot touch; lane: no added
