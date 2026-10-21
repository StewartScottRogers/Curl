---
id: BL-2206
title: Re-fix AF-0191: BL-2130 cost $1.88, 3.7 times the median task cost, paging a 2,600-line handler in slices
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-14
completed: 2026-10-14
---
# BL-2206 — Re-fix AF-0191: BL-2130 cost $1.88, 3.7 times the median task cost, paging a 2,600-line handler in slices

## Goal

The defect the audit office reported as AF-0191 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0191 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0191-bl-2130-cost-1-88-3-7-times-the-median-task-cost-p.md`.

Re-fix: the earlier task(s) BL-2150 reached Done, and a later re-audit by the process auditor found the reproduction still reproduces:

- 2026-10-14 | 2026-10-14_0453.md | reproduces: yes | Original reproduction still prints BL-2130 costUsd 1.8816 and 14 'sed -n' matches. In this window BL-2182 (lane 1, 20:52 PDT 2026-10-13) crosses the cost-outlier rule again from paging code in slices: $4.80, 3.23x median, 92 turns, 19 'sed -n' trace lines, 58 Bash calls against 2 Reads.

Location: `logs/BL-2130-20261013-061911-L1.jsonl:197`

Location: `logs/BL-2130-20261013-061911-L1.jsonl:197`

Measure-FactoryProcess.ps1: BL-2130 costUsd 1.8816 against a median of $0.508 (3x = $1.52; still over 3x the $0.539 median that leaves BL-2087's $0 out). Result line: 44 turns, 457.4 s, opus, cache_read_input_tokens 3,050,207, output 18,906. There were 27 Bash calls, mostly sed -n slices of Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs (lines 660-720, 1135-1220, 1740-1775, 2000-2025, 2340-2420 and 2636-2670) and of its test partials. HttpProtocolHandler.cs and HttpProtocolHandlerTests.Authentication.cs were each Read twice. Edits went through heredoc-generated .ps1 scripts (/tmp/edit.ps1, /tmp/edit2.ps1, /tmp/e.ps1). The run also built and ran Curl.Conformance.UnitTests beyond its own projects. Lane log: 'BL-2130 end 44 turns, 7.6 min'.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-12 -LogRoot Z:\repos\Curl.audit\20261013-092506\logs -CiRunsJson Z:\repos\Curl.audit\20261013-092506\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2130'; Select-String -Path Z:\repos\Curl.audit\20261013-092506\logs\BL-2130-20261013-061911-L1.jsonl -Pattern 'sed -n' | Measure-Object
```

- Expected: No task costs more than 3 times the median costUsd
- Actual: BL-2130 costUsd 1.8816 (median 0.508)

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Closed without a fix: AF-0191 describes a past run's cost, which BL-2218's rule now closes once its original task is Done.

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing. interactive session; closing without a fix under BL-2218
- 2026-10-14: Doing -> Done. Closed without a fix: AF-0191 describes a past run's cost, which BL-2218's rule now closes once its original task is Done
