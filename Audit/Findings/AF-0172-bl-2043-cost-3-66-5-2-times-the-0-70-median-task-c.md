---
id: AF-0172
title: BL-2043 cost $3.66, 5.2 times the $0.70 median task cost: 75 turns and a rebase-conflict run
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-10-12_1653.md: the reproduction no longer reproduces.
key: process:BL-2043:BL-2043:cost-outlier
reproduction: none
task: BL-2107
tasks: BL-2107
found: 2026-10-11
found-at: d8245edb7b661211c6628e9062e9939934939fb2
scorecard: 2026-10-11_1638.md
duplicate-of:
closed: 2026-10-12
closed-how: reliable-reaudit
closed-by: 2026-10-12_1653.md
---
# AF-0172 - BL-2043 cost $3.66, 5.2 times the $0.70 median task cost: 75 turns and a rebase-conflict run

## Summary

Low finding from the process auditor at `logs/BL-2043-20261011-040525-L2.jsonl`: BL-2043 cost $3.66, 5.2 times the $0.70 median task cost: 75 turns and a rebase-conflict run.

## Evidence

Location: `logs/BL-2043-20261011-040525-L2.jsonl`

The tasks rows' median costUsd is 0.702 (n=54), so 3 times the median is $2.11. BL-2043 (Re-fix AF-0144, opus, lane 2) cost $3.6647 over 24.25 min. It is the only task over the bar. The five costliest are BL-2043 $3.66, BL-2092 $1.81, BL-2091 $1.75, BL-2076 $1.67 (2 claims) and BL-2090 $1.61. BL-2043's main run had result num_turns 75, total_cost_usd 3.32 and duration 1,289,834 ms, with 74 tool calls: Bash 43, Edit 16, PowerShell 9, Read 5, Write 1. DarkFactory-20261011-040525-L2.log shows 14 edits to HttpResponseBodyReader.cs between 04:13:59 and 04:24:11. A test pass of 2004/2007 at 04:22:46 sent it back for 4 more edits. The task was then moved to Done four times (04:27:11-04:27:50, 'move Done ok' each). Integration then hit 'git rebase: Could not apply bb400690d', which cost a second, resolve run of 6 turns and $0.345 (BL-2043-20261011-040525-L2-resolve.jsonl). The cost came from iterative edit/test turns on one file plus the conflict resolution.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-11 -LogRoot Z:\repos\Curl.audit\20261011-163836\logs -CiRunsJson Z:\repos\Curl.audit\20261011-163836\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Sort-Object costUsd -Descending | Select-Object -First 5
```

- Expected: No task over 3 times the median costUsd (about $2.11).
- Actual: BL-2043 costUsd 3.6647, minutes 24.25, done; next is BL-2092 at 1.8139.

## Re-audits

- 2026-10-12 | 2026-10-12_1653.md | reproduces: no | The original reproduction still lists BL-2043 at $3.66 against a $0.71 median in the old window. In this window (14 tasks done), the median task costs $0.978 and the costliest is BL-2087 at $1.63 (1.67 times), followed by BL-2105 $1.63, BL-2115 $1.46, BL-2114 $1.13 and BL-2116 $1.11. No task exceeds 3 times the median.

## Log

- 2026-10-11: filed proposed.
- 2026-10-11: proposed -> accepted.
- 2026-10-12: accepted -> closed. Re-audit 2026-10-12_1653.md: the reproduction no longer reproduces.
