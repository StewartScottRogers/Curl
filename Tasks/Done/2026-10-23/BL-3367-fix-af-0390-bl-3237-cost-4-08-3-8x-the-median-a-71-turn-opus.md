---
id: BL-3367
title: Fix AF-0390: BL-3237 cost $4.08 (3.8x the median): a 71-turn Opus feature run plus two rebase-conflict resolve runs
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3367 — Fix AF-0390: BL-3237 cost $4.08 (3.8x the median): a 71-turn Opus feature run plus two rebase-conflict resolve runs

## Goal

What drove the past run's cost that the audit office reported as AF-0390 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0390 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0390-bl-3237-cost-4-08-3-8x-the-median-a-71-turn-opus-f.md`.

Location: `logs/BL-3237-20261023-035126-L1.jsonl`

Location: `logs/BL-3237-20261023-035126-L1.jsonl`

Main run: Opus, 71 turns, 29.7 min, $3.64 (04:39:56 to 05:09:43). Two resolve runs: 12 turns $0.31 and 17 turns $0.44. Each resolve run reran the whole fast suite (19221/19221 and 20742/20742 pass). The lane trace shows two failed test turns: 'test FAIL grep: C:\Users\Stewart Rogers\AppData\Local\Temp/...' at 04:50:09, and 'test FAIL Permission to use Bash with command cd /z/repos/C...' at 05:08:56, where a disallowed shell command cost a retry.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261023-061815\logs -CiRunsJson Z:\repos\Curl.audit\20261023-061815\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261023-061815\logs\DarkFactory-20261023-035126-L1.log -Pattern 'BL-3237 (claim|test|end|resolve)'
```

- Expected: costUsd at most 3x the median ($3.19).
- Actual: tasks[BL-3237].costUsd = 4.0802

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [x] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause (from the lane trace, DarkFactory-20261023-035126-L1.log): $3.64 of the $4.08 is the main Opus feature-pipeline run (71 turns, 29.7 min) on a task that replays Libcurl easy-handle cases in the differential run; feature runs on opus are the factory model rule, so that is the task's size, not waste. The two failed test turns (a grep path and one disallowed `cd` Bash command) each cost one retry turn. The two resolve runs ($0.31 and $0.44) were concurrent-lane rebase conflicts; the 19221 and 20742 fast-suite lines are the harness's own post-rebase verification (DarkFactory/Integrate.ps1), not resolver choice, and that check is required.
- Verdict: unavoidable, no code fix. 3.8x the median is one large feature task plus two conflicts; nothing in RunDarkFactory.ps1 to change.
- Build and fast tests: no code changed in this run; the previous commit on this branch was green (the shift verifies integration).

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
- 2026-10-23: Doing -> Done. AF-0390 cause named: one large Opus feature run plus two concurrent-lane rebase resolves, unavoidable, no fix needed
