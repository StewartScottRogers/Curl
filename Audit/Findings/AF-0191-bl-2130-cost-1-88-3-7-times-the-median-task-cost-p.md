---
id: AF-0191
title: BL-2130 cost $1.88, 3.7 times the median task cost, paging a 2,600-line handler in slices
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:BL-2130:cost-outlier
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
# AF-0191 - BL-2130 cost $1.88, 3.7 times the median task cost, paging a 2,600-line handler in slices

## Summary

Low finding from the process auditor at `logs/BL-2130-20261013-061911-L1.jsonl:197`: BL-2130 cost $1.88, 3.7 times the median task cost, paging a 2,600-line handler in slices.

## Evidence

Location: `logs/BL-2130-20261013-061911-L1.jsonl:197`

Measure-FactoryProcess.ps1: BL-2130 costUsd 1.8816 against a median of $0.508 (3x = $1.52; still over 3x the $0.539 median that leaves BL-2087's $0 out). Result line: 44 turns, 457.4 s, opus, cache_read_input_tokens 3,050,207, output 18,906. There were 27 Bash calls, mostly sed -n slices of Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs (lines 660-720, 1135-1220, 1740-1775, 2000-2025, 2340-2420 and 2636-2670) and of its test partials. HttpProtocolHandler.cs and HttpProtocolHandlerTests.Authentication.cs were each Read twice. Edits went through heredoc-generated .ps1 scripts (/tmp/edit.ps1, /tmp/edit2.ps1, /tmp/e.ps1). The run also built and ran Curl.Conformance.UnitTests beyond its own projects. Lane log: 'BL-2130 end 44 turns, 7.6 min'.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-12 -LogRoot Z:\repos\Curl.audit\20261013-092506\logs -CiRunsJson Z:\repos\Curl.audit\20261013-092506\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2130'; Select-String -Path Z:\repos\Curl.audit\20261013-092506\logs\BL-2130-20261013-061911-L1.jsonl -Pattern 'sed -n' | Measure-Object
```

- Expected: No task costs more than 3 times the median costUsd
- Actual: BL-2130 costUsd 1.8816 (median 0.508)

## Re-audits

## Log

- 2026-10-13: filed proposed.
- 2026-10-13: proposed -> accepted.
