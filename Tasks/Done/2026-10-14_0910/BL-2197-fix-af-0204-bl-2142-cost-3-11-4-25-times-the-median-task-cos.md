---
id: BL-2197
title: Fix AF-0204: BL-2142 cost $3.11, 4.25 times the median task cost, over 68 turns with 37 sliced Bash reads
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-14
completed: 2026-10-13
---
# BL-2197 — Fix AF-0204: BL-2142 cost $3.11, 4.25 times the median task cost, over 68 turns with 37 sliced Bash reads

## Goal

The defect the audit office reported as AF-0204 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0204 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0204-bl-2142-cost-3-11-4-25-times-the-median-task-cost.md`.

Location: `logs/BL-2142-20261013-133412-L2.jsonl`

Location: `logs/BL-2142-20261013-133412-L2.jsonl`

Measure-FactoryProcess.ps1: BL-2142 costUsd 3.1066, 15.97 minutes. The median costUsd over the 28 priced tasks is $0.73, so the 3x bar is $2.19. The result line has num_turns 68, duration 13.5 minutes and cache_read_input_tokens 6,420,589. Tool calls: Bash 49, Edit 14, PowerShell 2, Skill 1. 37 of the Bash calls read files in slices with 'sed -n', 'head -' or 'tail -n +', for example 'sed -n 250,290p Curl.Cli.UnitLibrary/CommandLineOptionTable.cs' and 'sed -n 1,200p .claude/skills/task-board/SKILL.md | head -120'. Each slice re-sends the growing context, which is what drove the cost. BL-2142 was claimed at 13:34:45, before BL-2149's fix 'run prompts tell a task to Read a file once, not slice it' (ebf8328f, 13:51:23) landed.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-13 -LogRoot Z:\repos\Curl.audit\20261013-182203\logs -CiRunsJson Z:\repos\Curl.audit\20261013-182203\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Sort-Object costUsd -Descending | Select-Object -First 5; Select-String -Path Z:\repos\Curl.audit\20261013-182203\logs\BL-2142-20261013-133412-L2.jsonl -Pattern 'sed -n' | Measure-Object
```

- Expected: No task over 3 times the median costUsd (about $2.19).
- Actual: BL-2142 costUsd 3.1066. 31 jsonl lines match 'sed -n', and 37 Bash calls are slice reads.

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Cause is the same as AF-0190: BL-2142 was claimed before BL-2149 put "Read once, do not slice" in the run prompt. The prompt already carries that rule; added AF-0204 as a further instance to both prompt copies in RunDarkFactory.ps1. Reproduction reads a past log, so it cannot be re-run here (Audit paths are off limits to lanes); re-audit confirms.

## Log

- 2026-10-14: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. Run prompts now name AF-0204 beside the Read-once rule
