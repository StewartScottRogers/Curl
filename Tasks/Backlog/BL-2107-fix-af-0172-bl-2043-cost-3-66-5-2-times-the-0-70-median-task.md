---
id: BL-2107
title: Fix AF-0172: BL-2043 cost $3.66, 5.2 times the $0.70 median task cost: 75 turns and a rebase-conflict run
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-12
completed:
---
# BL-2107 — Fix AF-0172: BL-2043 cost $3.66, 5.2 times the $0.70 median task cost: 75 turns and a rebase-conflict run

## Goal

The defect the audit office reported as AF-0172 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0172 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0172-bl-2043-cost-3-66-5-2-times-the-0-70-median-task-c.md`.

Location: `logs/BL-2043-20261011-040525-L2.jsonl`

Location: `logs/BL-2043-20261011-040525-L2.jsonl`

The tasks rows' median costUsd is 0.702 (n=54), so 3 times the median is $2.11. BL-2043 (Re-fix AF-0144, opus, lane 2) cost $3.6647 over 24.25 min. It is the only task over the bar. The five costliest are BL-2043 $3.66, BL-2092 $1.81, BL-2091 $1.75, BL-2076 $1.67 (2 claims) and BL-2090 $1.61. BL-2043's main run had result num_turns 75, total_cost_usd 3.32 and duration 1,289,834 ms, with 74 tool calls: Bash 43, Edit 16, PowerShell 9, Read 5, Write 1. DarkFactory-20261011-040525-L2.log shows 14 edits to HttpResponseBodyReader.cs between 04:13:59 and 04:24:11. A test pass of 2004/2007 at 04:22:46 sent it back for 4 more edits. The task was then moved to Done four times (04:27:11-04:27:50, 'move Done ok' each). Integration then hit 'git rebase: Could not apply bb400690d', which cost a second, resolve run of 6 turns and $0.345 (BL-2043-20261011-040525-L2-resolve.jsonl). The cost came from iterative edit/test turns on one file plus the conflict resolution.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-11 -LogRoot Z:\repos\Curl.audit\20261011-163836\logs -CiRunsJson Z:\repos\Curl.audit\20261011-163836\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Sort-Object costUsd -Descending | Select-Object -First 5
```

- Expected: No task over 3 times the median costUsd (about $2.11).
- Actual: BL-2043 costUsd 3.6647, minutes 24.25, done; next is BL-2092 at 1.8139.

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-12: Created.
