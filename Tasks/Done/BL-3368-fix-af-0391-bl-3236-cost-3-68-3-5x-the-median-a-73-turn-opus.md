---
id: BL-3368
title: Fix AF-0391: BL-3236 cost $3.68 (3.5x the median): a 73-turn Opus feature run plus a rebase-conflict resolve run
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3368 — Fix AF-0391: BL-3236 cost $3.68 (3.5x the median): a 73-turn Opus feature run plus a rebase-conflict resolve run

## Goal

What drove the past run's cost that the audit office reported as AF-0391 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0391 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0391-bl-3236-cost-3-68-3-5x-the-median-a-73-turn-opus-f.md`.

Location: `logs/BL-3236-20261023-035126-L2.jsonl`

Location: `logs/BL-3236-20261023-035126-L2.jsonl`

Main run: Opus, 73 turns, 17.9 min, $3.32 (04:49:00 to 05:06:57). Resolve run: 14 turns, 5.6 min, $0.36. The verify step then ran all 45 test projects because an IntegrationTests project changed. Cost is driven by turn count on Opus for a differential-replay port. The task was routed to Opus as a 'feature pipeline' task.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261023-061815\logs -CiRunsJson Z:\repos\Curl.audit\20261023-061815\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261023-061815\logs\DarkFactory-20261023-035126-L2.log -Pattern 'BL-3236 (claim|end|resolve|verify)'
```

- Expected: costUsd at most 3x the median ($3.19).
- Actual: tasks[BL-3236].costUsd = 3.681

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [x] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause (AF-0391): turn count on Opus. BL-3236 was a differential-replay port on the feature pipeline, so ModelChoice.ps1 routed it to Opus (Get-FeatureModelChoice: only routine titles get Sonnet); 73 turns at Opus rates gave $3.32, plus a $0.36 rebase-conflict resolve run. Both are inherent to a large port.
- The "ran all 45 test projects" step is already addressed: Get-AffectedTestProjects (BL-2967, DarkFactory/AffectedTests.ps1) selects only affected *.UnitTests projects, and IntegrationTests are not run under the fast filter. All-projects runs only when a shared build file or .csproj changed.
- Recorded as unavoidable; no avoidable waste found, no code changed. The raw logs/BL-3236 log is not in this checkout, so this rests on the finding and the routing code. Build clean, 46 fast-test projects passed, none failed.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
- 2026-10-23: Doing -> Done. AF-0391 cost cause named (Opus turn count on a large feature port); recorded unavoidable, selective test run already in place
