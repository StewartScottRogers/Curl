---
id: AF-0191
title: BL-2130 cost $1.88, 3.7 times the median task cost, paging a 2,600-line handler in slices
auditor: process
severity: Low
status: closed
reason: Closed by an interactive session under ADR-0422's 2026-10-14 amendment (Stewart, BL-2218): a cost-outlier finding records a past run's cost, which no fix can change; its task BL-2150 is Done at work/dark-factory 6d3225a967b4, so it closes without a re-audit. Re-fix task BL-2206, filed before the rule, closes without a fix.
key: process:logs:BL-2130:cost-outlier
reproduction: none
task: BL-2150
tasks: BL-2150
found: 2026-10-13
found-at: c86d04eeb8477108eb6d9195bb036a4ddaecc5fc
scorecard: 2026-10-13_0925.md
duplicate-of:
closed: 2026-10-14
closed-how: task-done-historical
closed-by: session
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

- 2026-10-13 | 2026-10-13_1822.md | reproduces: yes | The given reproduction still shows BL-2130 costUsd 1.8816 and 14 'sed -n' lines over the old window. Re-audited by mechanism over this window: BL-2152 (lane 2, claimed 13:58:41, after BL-2149's read-once prompt fix at 13:51:23) cost $2.46, 3.4 times the median. It paged HandBuiltTlsProvider.cs in slices ('sed -n 560,660p', 'sed -n 765,790p', 'sed -n 805,820p', and Read at offset 574), with 8 offset Reads and 12 sliced Bash calls.
- 2026-10-14 | 2026-10-14_0453.md | reproduces: yes | Original reproduction still prints BL-2130 costUsd 1.8816 and 14 'sed -n' matches. In this window BL-2182 (lane 1, 20:52 PDT 2026-10-13) crosses the cost-outlier rule again from paging code in slices: $4.80, 3.23x median, 92 turns, 19 'sed -n' trace lines, 58 Bash calls against 2 Reads.

## Log

- 2026-10-13: filed proposed.
- 2026-10-13: proposed -> accepted.
- 2026-10-14: accepted -> closed. Closed by an interactive session under ADR-0422's 2026-10-14 amendment (Stewart, BL-2218): a cost-outlier finding records a past run's cost, which no fix can change; its task BL-2150 is Done at work/dark-factory 6d3225a967b4, so it closes without a re-audit. Re-fix task BL-2206, filed before the rule, closes without a fix.
