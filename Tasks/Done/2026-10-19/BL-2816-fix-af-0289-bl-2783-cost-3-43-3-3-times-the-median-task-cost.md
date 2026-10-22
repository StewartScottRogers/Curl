---
id: BL-2816
title: Fix AF-0289: BL-2783 cost $3.43, 3.3 times the median task cost of $1.04 (12 builds, 2 of them failed on tooling)
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-19
completed: 2026-10-19
---
# BL-2816 — Fix AF-0289: BL-2783 cost $3.43, 3.3 times the median task cost of $1.04 (12 builds, 2 of them failed on tooling)

## Goal

What drove the past run's cost that the audit office reported as AF-0289 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0289 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0289-bl-2783-cost-3-43-3-3-times-the-median-task-cost-o.md`.

Location: `logs/DarkFactory-20261018-103803-L1.log`

Location: `logs/DarkFactory-20261018-103803-L1.log`

Measure-FactoryProcess.ps1 -Since 2026-10-18: BL-2783 claims=1 minutes=20.87 costUsd=3.4339 outcome=done; threshold 3.1176. BL-2783-20261018-103803-L1.jsonl result: num_turns=69, 7672177 cache-read tokens, 67 tool calls (46 Bash), model opus. The lane log shows 12 build lines between 16:30:42 and 16:47:27. Two of them failed on the environment, not the code: '16:32:28 L1 BL-2783 build FAIL Permission to use Bash with command head -30; git~' and '16:34:34 L1 BL-2783 build FAIL rm: cannot remove 'Curl.Conformance.UnitTests/bin~'. That is avoidable waste: a denied command and a locked bin folder each cost a rebuild turn, and 10 builds ran for one change.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-18 -LogRoot Z:\repos\Curl.audit\20261018-190531\logs -CiRunsJson Z:\repos\Curl.audit\20261018-190531\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | ? id -eq 'BL-2783'; Select-String -Path Z:\repos\Curl.audit\20261018-190531\logs\DarkFactory-20261018-103803-L1.log -Pattern 'BL-2783 build'
```

- Expected: costUsd at most 3 times the median (3.1176).
- Actual: BL-2783 costUsd 3.4339; 12 build lines, 2 FAIL on permission denial and a locked bin folder.

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [x] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause (2026-10-19): BL-2783 ran on opus for 69 turns, 67 tool calls (46 Bash) and 7.67M cache-read tokens; cost scales with turns times context, so 3.3x the median comes from a long opus run, not from one runaway step. The two failed builds were a model-written compound Bash command (`head -30; git ...`) refused by the permission rules, and an `rm` of a `bin` folder another lane's process held open. Together they are about 2 of 69 turns (under 3% of cost), far too small to explain the overrun.
- Decision: recorded as unavoidable, no change to `RunDarkFactory.ps1`. The run prompt already forbids slice reads, reruns and process killing (AF-0190, AF-0204, AF-0268); a denied command or a locked folder in a parallel-lane checkout is environmental and a rule against it would not change a rebuild turn. The finding is a past run's cost (`:cost-outlier`), closed by the first audit after Done.

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
- 2026-10-19: Doing -> Done. Cost cause named (long opus run, 2 env-failed builds under 3 percent) and recorded as unavoidable; build and fast tests green
