---
id: BL-2149
title: Fix AF-0190: BL-2131 cost $3.11, 6.1 times the median task cost, over 70 turns of sliced code reading
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-13
completed:
---
# BL-2149 — Fix AF-0190: BL-2131 cost $3.11, 6.1 times the median task cost, over 70 turns of sliced code reading

## Goal

The defect the audit office reported as AF-0190 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0190 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0190-bl-2131-cost-3-11-6-1-times-the-median-task-cost-o.md`.

Location: `logs/BL-2131-20261013-061911-L1.jsonl:316`

Location: `logs/BL-2131-20261013-061911-L1.jsonl:316`

Measure-FactoryProcess.ps1: BL-2131 costUsd 3.1093 against a median of $0.508 over the 10 Done tasks (3x = $1.52). The 5 costliest: BL-2131 $3.11, BL-2130 $1.88, BL-2119 $0.82, BL-2121 $0.57, BL-2124 $0.54. Result line: 70 turns, 673.7 s, opus, cache_read_input_tokens 7,044,765, cache_creation 136,855, output 30,249. The tool calls were 56 Bash, 8 Edit, 2 PowerShell, 1 Read and 1 Skill. Most Bash calls were sed -n / grep slices of HttpRequestBodyWriter.cs, TransferProgressRecorder.cs, HttpProtocolHandlerTests*.cs and Record-CurlExchange.ps1, each one turn re-reading a 100k+ context. Waste inside the run: a perl edit of CurlCommandRunnerAuthRetryProgressMeterTests.cs undone with git checkout and git restore; two perl rewrites of its own task's touches mid-run; a git fetch to read Doing touches; the lane log shows 'move Done FAIL PreToolUse:Bash hook error' (06:50:01) before the move succeeded, and 'git push FAIL ... fast-forwards' (06:51:31) before integration.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-12 -LogRoot Z:\repos\Curl.audit\20261013-092506\logs -CiRunsJson Z:\repos\Curl.audit\20261013-092506\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2131'; Select-String -Path Z:\repos\Curl.audit\20261013-092506\logs\DarkFactory-20261013-061911-L1.log -Pattern 'BL-2131 end|FAIL'
```

- Expected: No task costs more than 3 times the median costUsd
- Actual: BL-2131 costUsd 3.1093 (median 0.508); 'BL-2131 end 70 turns, 11.2 min'; move Done FAIL and git push FAIL lines

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
