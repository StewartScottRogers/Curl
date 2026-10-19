---
id: BL-2813
title: Fix AF-0286: BL-2652 cost $6.52, 6.3 times the median task cost of $1.04 (lib1560 port, 110 turns)
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-19
completed:
---
# BL-2813 — Fix AF-0286: BL-2652 cost $6.52, 6.3 times the median task cost of $1.04 (lib1560 port, 110 turns)

## Goal

What drove the past run's cost that the audit office reported as AF-0286 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0286 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0286-bl-2652-cost-6-52-6-3-times-the-median-task-cost-o.md`.

Location: `logs/BL-2652-20261018-103803-L1.jsonl`

Location: `logs/BL-2652-20261018-103803-L1.jsonl`

Measure-FactoryProcess.ps1 -Since 2026-10-18: BL-2652 claims=1 minutes=31.5 costUsd=6.5191 outcome=done; median costUsd over 89 claimed tasks is 1.0392 (threshold 3.1176); median over the 71 done tasks is 1.0498. The run result shows num_turns=110, 99035 output tokens, 13287036 cache-read tokens, 235041 cache-creation tokens, 109 tool calls (22 Read, 48 Bash), model opus (feature pipeline). Lane log DarkFactory-20261018-103803-L1.log has 11:09:58 claim 'Port libtest program 1560 (the URL API test, 2549 lines ...', 25 edit and 8 write lines, and test passes climbing 26/29 -> 27/29 -> 1163/1164 -> ok, then a final 'test 0/1 pass' at 11:39:24 before DONE at 11:41:28. The cost came from the size of the port (99k output tokens) and from fix-and-retest cycles with the whole context re-read each turn. Ranked by cost, this is the most expensive of the 5 costliest tasks.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-18 -LogRoot Z:\repos\Curl.audit\20261018-190531\logs -CiRunsJson Z:\repos\Curl.audit\20261018-190531\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Sort-Object costUsd -Descending | Select-Object -First 5; Select-String -Path Z:\repos\Curl.audit\20261018-190531\logs\DarkFactory-20261018-103803-L1.log -Pattern 'BL-2652 (claim|test|DONE)'
```

- Expected: No task's costUsd is over 3 times the median costUsd.
- Actual: BL-2652 costUsd 6.5191 against a median of 1.0392 (6.3 times).

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [ ] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-19: Created.
