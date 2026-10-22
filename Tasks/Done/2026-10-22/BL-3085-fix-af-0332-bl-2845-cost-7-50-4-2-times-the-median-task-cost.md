---
id: BL-3085
title: Fix AF-0332: BL-2845 cost $7.50, 4.2 times the median task cost of $1.80: a full first run thrown away by a requeue, then an 80-turn second run and a rebase-conflict resolve
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/Integrate.ps1]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3085 — Fix AF-0332: BL-2845 cost $7.50, 4.2 times the median task cost of $1.80: a full first run thrown away by a requeue, then an 80-turn second run and a rebase-conflict resolve

## Goal

What drove the past run's cost that the audit office reported as AF-0332 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0332 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0332-bl-2845-cost-7-50-4-2-times-the-median-task-cost-o.md`.

Location: `logs/DarkFactory-20261021-140711-L9.log:2`

Location: `logs/DarkFactory-20261021-140711-L9.log:2`

Runs (from the jsonl result lines): BL-2845-20261021-064209-L1 46 turns $2.54, then REQUEUE 07:50:15 'Waits on BL-2899 to BL-2907' (ten split tasks it depended on); BL-2845-20261021-140711-L9 80 turns $4.55, 9.2M cache-read tokens, 48 Bash calls; BL-2845-20261021-140711-L9-resolve 8 turns $0.41 for a rebase conflict. The $2.54 of the first run bought only a dependency discovery that planning could have made before the claim.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-19 -LogRoot Z:\repos\Curl.audit\20261022-013026\logs -CiRunsJson Z:\repos\Curl.audit\20261022-013026\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Sort-Object costUsd -Descending | Select-Object -First 5
```

- Expected: No task over 3 times the median costUsd (median 1.8006, so 5.40).
- Actual: BL-2845 7.4985, BL-2938 5.7798, BL-2970 5.602, BL-2924 5.5624, BL-2859 5.4413

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [x] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Cause (BL-2845, $7.50), three parts. (1) The first run (46 turns, $2.54) only found that the port needed ten split tasks (BL-2899 to BL-2907) and requeued: the AF-0147 pattern. The lane prompt already requires the dependency, size and hook pass before the first edit (DarkFactory/Prompts.ps1, rule 4), so that cause is already fixed. (2) The 80-turn second run ($4.55) was the real work of a large upstream-port task: unavoidable. (3) The 8-turn rebase-conflict resolve ($0.41) is the shift's normal integration path when two lanes touch one file, and is cheap. No avoidable waste is left in DarkFactory/Integrate.ps1, so no code changed.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
- 2026-10-22: Doing -> Done. Cost cause named: requeue already prevented by the AF-0147 prompt rule, rest unavoidable; no code change
