---
id: BL-4235
title: Fix AF-0482: BL-3563 cost $9.19 (8.6x the $1.07 median) over three opus runs, the last stopped at the 80-turn cap
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1, DarkFactory/Prompts.ps1, DarkFactory/TaskRun.ps1]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4235 — Fix AF-0482: BL-3563 cost $9.19 (8.6x the $1.07 median) over three opus runs, the last stopped at the 80-turn cap

## Goal

What drove the past run's cost that the audit office reported as AF-0482 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0482 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0482-bl-3563-cost-9-19-8-6x-the-1-07-median-over-three.md`.

Location: `logs/BL-3563-20261028-052757-L1.jsonl`

Location: `logs/BL-3563-20261028-052757-L1.jsonl`

Runs: BL-3563-20261026-063535-L5 (17 turns, 7.7 min), BL-3563-20261026-151632-L5 (62 turns, 33.7 min) and BL-3563-20261028-052757-L1 (81 turns, 53.3 min, stopped at the 80-turn cap; DarkFactory-20261028-052757-L1.log:53). Cost was driven by re-reading the task's scope on every claim and by a final run that hit the cap before the task was split. Two of the three runs ended on dependencies found mid-run. Deferred and split into BL-3976 to BL-3978.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).tasks | ? id -eq 'BL-3563'
```

- Expected: No task over 3x the median costUsd and over 5 USD.
- Actual: BL-3563 costUsd 9.1885, claims 3, outcome requeued.

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [x] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Measured from the audit copy of the logs: run 1 $0.64 (17 turns, requeued for BL-3846/BL-3847, found in its first pass as it should be); run 2 $3.37 (62 turns, did the multi wiring, then found BL-3890 needed for `C_65`, a missing prerequisite the first pass did not see); run 3 $5.17 (81 turns, `error_max_turns`). Run 3 spent turns 1-17 merging the stash with BL-3890's changes, 18-50 fixing and extending the tests, 51-73 a real-libcurl probe (all five cases matched), and 74-97 four Measure-CodeQuality.ps1 runs that each turned up a failure (test582, a 200 ms settle race under coverage). The cap stopped it mid quality run with the fix passing. The split run then deferred the task into three subtasks, so a fourth claim (BL-3977) redid the landing.
- Cause: the avoidable waste was in run 3. The lane prompt tells a run its time deadline and asks it to hand back what is left before then (AF-0094), but never told it about the 80-turn cap, so the run could not see the cap coming. Run 2's late prerequisite is the kind rule 4 already covers, so it needs no new fix; the rest of the cost was real work (stash merge, probe, quality fixes).
- Fix: `Get-TurnCapNote` (DarkFactory/TaskRun.ps1) fills a new `{TURNCAP}` placeholder after the deadline rule in both `$Prompt` and `$LanePrompt` (DarkFactory/Prompts.ps1). It names the cap and asks the run to hand back what is left 15 turns before it; with `-MaxRunTurns 0` it is empty. The 15-turn margin is a default I chose: a hand-back (file the task, write Notes, move it, commit) takes about 5 to 8 turns, which leaves room to spare. `-TestLaneFlags` checks it (all PASS).
- Touches widened to DarkFactory/Prompts.ps1 and DarkFactory/TaskRun.ps1, where the prompt is built. No task in Doing on origin/work/dark-factory names either file. RunDarkFactory.ps1's help now says the prompts name the cap.
- No C# changed, so no Measure-CodeQuality.ps1 run was needed.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
- 2026-10-29: Doing -> Done. Lane and task prompts now name the -MaxRunTurns cap and ask runs to hand back 15 turns before it (AF-0482)
