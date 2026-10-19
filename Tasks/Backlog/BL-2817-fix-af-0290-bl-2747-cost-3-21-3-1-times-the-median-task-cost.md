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
completed:
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

- [ ] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-19: Created.
