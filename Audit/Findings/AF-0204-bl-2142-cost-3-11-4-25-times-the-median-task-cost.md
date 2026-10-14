---
id: AF-0204
title: BL-2142 cost $3.11, 4.25 times the median task cost, over 68 turns with 37 sliced Bash reads
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:BL-2142:cost-outlier
reproduction: none
task: BL-2197
tasks: BL-2197
found: 2026-10-13
found-at: c192c3e80e58636be1d282d241a7781f7df86695
scorecard: 2026-10-13_1822.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0204 - BL-2142 cost $3.11, 4.25 times the median task cost, over 68 turns with 37 sliced Bash reads

## Summary

Low finding from the process auditor at `logs/BL-2142-20261013-133412-L2.jsonl`: BL-2142 cost $3.11, 4.25 times the median task cost, over 68 turns with 37 sliced Bash reads.

## Evidence

Location: `logs/BL-2142-20261013-133412-L2.jsonl`

Measure-FactoryProcess.ps1: BL-2142 costUsd 3.1066, 15.97 minutes. The median costUsd over the 28 priced tasks is $0.73, so the 3x bar is $2.19. The result line has num_turns 68, duration 13.5 minutes and cache_read_input_tokens 6,420,589. Tool calls: Bash 49, Edit 14, PowerShell 2, Skill 1. 37 of the Bash calls read files in slices with 'sed -n', 'head -' or 'tail -n +', for example 'sed -n 250,290p Curl.Cli.UnitLibrary/CommandLineOptionTable.cs' and 'sed -n 1,200p .claude/skills/task-board/SKILL.md | head -120'. Each slice re-sends the growing context, which is what drove the cost. BL-2142 was claimed at 13:34:45, before BL-2149's fix 'run prompts tell a task to Read a file once, not slice it' (ebf8328f, 13:51:23) landed.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-13 -LogRoot Z:\repos\Curl.audit\20261013-182203\logs -CiRunsJson Z:\repos\Curl.audit\20261013-182203\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Sort-Object costUsd -Descending | Select-Object -First 5; Select-String -Path Z:\repos\Curl.audit\20261013-182203\logs\BL-2142-20261013-133412-L2.jsonl -Pattern 'sed -n' | Measure-Object
```

- Expected: No task over 3 times the median costUsd (about $2.19).
- Actual: BL-2142 costUsd 3.1066. 31 jsonl lines match 'sed -n', and 37 Bash calls are slice reads.

## Re-audits

## Log

- 2026-10-13: filed proposed.
- 2026-10-13: proposed -> accepted.
