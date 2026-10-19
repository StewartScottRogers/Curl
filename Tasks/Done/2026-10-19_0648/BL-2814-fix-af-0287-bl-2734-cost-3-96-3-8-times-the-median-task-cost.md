---
id: BL-2814
title: Fix AF-0287: BL-2734 cost $3.96, 3.8 times the median task cost of $1.04 (71 turns, 49 Bash calls)
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-19
completed: 2026-10-19
---
# BL-2814 — Fix AF-0287: BL-2734 cost $3.96, 3.8 times the median task cost of $1.04 (71 turns, 49 Bash calls)

## Goal

What drove the past run's cost that the audit office reported as AF-0287 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0287 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0287-bl-2734-cost-3-96-3-8-times-the-median-task-cost-o.md`.

Location: `logs/BL-2734-20261018-103803-L1.jsonl`

Location: `logs/BL-2734-20261018-103803-L1.jsonl`

Measure-FactoryProcess.ps1 -Since 2026-10-18: BL-2734 claims=1 minutes=34.17 costUsd=3.9613 outcome=done; threshold 3.1176 (3 times the median 1.0392). The run result shows num_turns=71, 9318792 cache-read tokens, 32714 output tokens, 69 tool calls of which 49 were Bash, model opus. Lane log L1 runs from 12:34:41 claim ('curl_url_set keeps an empty host under CURLU_NO_AUTHORITY ...') to 13:08:51 DONE, with 3 builds, 4 tests, 3 quality runs and 11 edits. Most of the cost is cache reads across 71 turns: build, test and quality cycles, each re-reading the whole context.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-18 -LogRoot Z:\repos\Curl.audit\20261018-190531\logs -CiRunsJson Z:\repos\Curl.audit\20261018-190531\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | ? id -eq 'BL-2734'
```

- Expected: costUsd at most 3 times the median (3.1176).
- Actual: BL-2734 costUsd 3.9613.

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [x] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Cause: unavoidable. BL-2734 was a feature-sized url fix (CURLU_NO_AUTHORITY, empty host and bad-port ordering) in a library held to the 100% coverage and complexity gates, so the run needed 3 builds, 4 test runs and 3 Measure-CodeQuality runs, each a turn re-reading a large context (cache reads 9.3M tokens). Those runs are required by the quality gates and AF-0213; no repeated reruns, subagents or slice-reading loops appear in the finding. Opus was chosen by the factory rule for the work type. Logs are an audit-reachable path no lane may read, so no further breakdown was possible. No change to RunDarkFactory.ps1.

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
- 2026-10-19: Doing -> Done. Cost of BL-2734 recorded as unavoidable quality-gate build/test/quality cycles
