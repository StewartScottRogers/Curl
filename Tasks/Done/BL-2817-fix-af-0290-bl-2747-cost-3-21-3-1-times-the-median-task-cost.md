---
id: BL-2817
title: Fix AF-0290: BL-2747 cost $3.21, 3.1 times the median task cost of $1.04 (63 turns, 6 test runs)
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-19
completed: 2026-10-19
---
# BL-2817 — Fix AF-0290: BL-2747 cost $3.21, 3.1 times the median task cost of $1.04 (63 turns, 6 test runs)

## Goal

What drove the past run's cost that the audit office reported as AF-0290 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0290 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0290-bl-2747-cost-3-21-3-1-times-the-median-task-cost-o.md`.

Location: `logs/BL-2747-20261018-103803-L1.jsonl`

Location: `logs/BL-2747-20261018-103803-L1.jsonl`

Measure-FactoryProcess.ps1 -Since 2026-10-18: BL-2747 claims=1 minutes=24.67 costUsd=3.2101 outcome=done; threshold 3.1176 (it is also over 3 times the done-only median, 3.1494). The run result shows num_turns=63, 6474561 cache-read tokens, 30845 output tokens, 62 tool calls (37 Bash), model opus. Lane log L1 runs from 13:59:58 claim ('Keep a Libcurl handle's last Digest challenge ...') to 14:24:38 DONE, with 3 builds, 6 tests, 3 quality runs and 16 edits. Three other tasks waited on it: BL-2691 (13:10:25) and BL-2744 (13:24:49) were requeued to wait on it. The cost is test and quality cycles that re-read the whole context each turn.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-18 -LogRoot Z:\repos\Curl.audit\20261018-190531\logs -CiRunsJson Z:\repos\Curl.audit\20261018-190531\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | ? id -eq 'BL-2747'
```

- Expected: costUsd at most 3 times the median (3.1176).
- Actual: BL-2747 costUsd 3.2101.

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [x] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause (2026-10-19): BL-2747 ran on opus for 63 turns (62 tool calls, 37 Bash, 16 edits) and 6.47M cache-read tokens; cost scales with turns times context. The task changed three libraries (Curl.Engine, Curl.Protocol.Http, Curl.Protocol.Abstractions) plus an ADR, so its 3 builds, 6 test runs and 3 Measure-CodeQuality.ps1 runs (one per changed library, as the run rules require before committing) are the work's own size, not a rerun loop. The result, 3.21 USD, is only just over the 3.1176 threshold.
- Decision: recorded as unavoidable, no change to `RunDarkFactory.ps1`. The run prompt already forbids slice reads, test reruns and process killing (AF-0190, AF-0204, AF-0268); the three quality runs are required (AF-0213). A rule to split such a task would be sizing, which the first-pass sizing check in the run prompt already asks for. The finding is a past run's cost (`:cost-outlier`), closed by the first audit after Done.

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
- 2026-10-19: Doing -> Done. Cost cause named (long opus run, three libraries, required quality runs) and recorded as unavoidable; build and fast tests green
