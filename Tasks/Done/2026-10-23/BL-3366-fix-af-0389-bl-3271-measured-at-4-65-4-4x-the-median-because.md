---
id: BL-3366
title: Fix AF-0389: BL-3271 measured at $4.65 (4.4x the median) because two different tasks shared its ID; the merged cost then tripped the cost cap and a split that failed
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3366 — Fix AF-0389: BL-3271 measured at $4.65 (4.4x the median) because two different tasks shared its ID; the merged cost then tripped the cost cap and a split that failed

## Goal

What drove the past run's cost that the audit office reported as AF-0389 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0389 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0389-bl-3271-measured-at-4-65-4-4x-the-median-because-t.md`.

Location: `logs/BL-3271-20261023-035126-L2.jsonl`

Location: `logs/BL-3271-20261023-035126-L2.jsonl`

Done-task median cost is $1.062, so the threshold is $3.19. The tool's $4.65 for BL-3271 adds up runs from two tasks with the same ID. L2's 'Land per-handle caches' (split from BL-3204) ran Opus for 44 turns, 25.4 min, $2.91. L3's 'Keep -Z URL order when the host's first connection stays alive' ran Sonnet for 86 turns, $1.59, plus a $0.15 resolve run. Both lanes logged a DONE for BL-3271 (L2 04:21:49, L3 04:30:54). At 04:19:42 L4 found BL-3271 'unclaimable cost cap: its runs of the last day already cost 3.69 of its 3.82'. That cap was computed on the merged cost. L4 renumbered 'BL-3271 -> BL-3276' at 04:20:12 and spent a split run ($0.18), whose push was refused ('split failed: the push of the split was refused; blocked', 04:22:53). The avoidable waste is the duplicate ID, not either run: neither task's own cost exceeds the threshold.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261023-061815\logs -CiRunsJson Z:\repos\Curl.audit\20261023-061815\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261023-061815\logs\DarkFactory-20261023-035126-L*.log -Pattern 'BL-3271 (claim|DONE|unclaimable|split)|renum'
```

- Expected: No task over 3x the median cost; one task per ID.
- Actual: tasks[BL-3271].costUsd = 4.6482; claims of two different titles under BL-3271 by L2 and L3.

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [x] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause: two different tasks were given the ID BL-3271 (L2's split of BL-3204, "Land per-handle caches", and L3's "Keep -Z URL order..."). The cost tool and the claim path's cost cap add runs by ID, so the merged $4.65 (L2 $2.91 Opus + L3 $1.59 + $0.15 resolve) tripped the cap at 04:19:42, and L4's renumber plus split run ($0.18) was refused on push. Neither task's own cost exceeded the $3.19 threshold.
- Avoidable waste: only the duplicate ID itself, the same cause as AF-0386 (BL-3263). Its fix (the board no longer giving out a duplicate ID) is BL-3363 in Backlog, which touches the same RunDarkFactory.ps1; fixing it here would duplicate that task, so no change is made in this one. The cost itself is otherwise unavoidable: each task was sized normally.
- The logs named by the finding are not in this checkout, so the numbers are taken from the task text.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
- 2026-10-23: Doing -> Done. AF-0389 cost named as a duplicate task ID; its fix is BL-3363, no change here
