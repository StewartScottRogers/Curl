---
id: AF-0190
title: BL-2131 cost $3.11, 6.1 times the median task cost, over 70 turns of sliced code reading
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:BL-2131:cost-outlier
reproduction: none
task: BL-2149
tasks: BL-2149
found: 2026-10-13
found-at: c86d04eeb8477108eb6d9195bb036a4ddaecc5fc
scorecard: 2026-10-13_0925.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0190 - BL-2131 cost $3.11, 6.1 times the median task cost, over 70 turns of sliced code reading

## Summary

Low finding from the process auditor at `logs/BL-2131-20261013-061911-L1.jsonl:316`: BL-2131 cost $3.11, 6.1 times the median task cost, over 70 turns of sliced code reading.

## Evidence

Location: `logs/BL-2131-20261013-061911-L1.jsonl:316`

Measure-FactoryProcess.ps1: BL-2131 costUsd 3.1093 against a median of $0.508 over the 10 Done tasks (3x = $1.52). The 5 costliest: BL-2131 $3.11, BL-2130 $1.88, BL-2119 $0.82, BL-2121 $0.57, BL-2124 $0.54. Result line: 70 turns, 673.7 s, opus, cache_read_input_tokens 7,044,765, cache_creation 136,855, output 30,249. The tool calls were 56 Bash, 8 Edit, 2 PowerShell, 1 Read and 1 Skill. Most Bash calls were sed -n / grep slices of HttpRequestBodyWriter.cs, TransferProgressRecorder.cs, HttpProtocolHandlerTests*.cs and Record-CurlExchange.ps1, each one turn re-reading a 100k+ context. Waste inside the run: a perl edit of CurlCommandRunnerAuthRetryProgressMeterTests.cs undone with git checkout and git restore; two perl rewrites of its own task's touches mid-run; a git fetch to read Doing touches; the lane log shows 'move Done FAIL PreToolUse:Bash hook error' (06:50:01) before the move succeeded, and 'git push FAIL ... fast-forwards' (06:51:31) before integration.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-12 -LogRoot Z:\repos\Curl.audit\20261013-092506\logs -CiRunsJson Z:\repos\Curl.audit\20261013-092506\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2131'; Select-String -Path Z:\repos\Curl.audit\20261013-092506\logs\DarkFactory-20261013-061911-L1.log -Pattern 'BL-2131 end|FAIL'
```

- Expected: No task costs more than 3 times the median costUsd
- Actual: BL-2131 costUsd 3.1093 (median 0.508); 'BL-2131 end 70 turns, 11.2 min'; move Done FAIL and git push FAIL lines

## Re-audits

## Log

- 2026-10-13: filed proposed.
- 2026-10-13: proposed -> accepted.
