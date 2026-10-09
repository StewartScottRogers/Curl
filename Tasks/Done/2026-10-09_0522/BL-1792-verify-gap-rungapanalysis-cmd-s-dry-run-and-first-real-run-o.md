---
id: BL-1792
title: Verify Gap\RunGapAnalysis.cmd's dry run and first real run outside the factory
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1740]
touches: [Gap/RunGapAnalysis.ps1, Gap/RunGapAnalysis.cmd]
lane: no
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1792 — Verify Gap\RunGapAnalysis.cmd's dry run and first real run outside the factory

## Goal

`Gap\RunGapAnalysis.cmd`'s dry run and a first real run are shown to work, from an
interactive session, and any fault they turn up is fixed.

## Context

BL-1740 wrote `Gap/RunGapAnalysis.ps1` in a dark factory lane. The script refuses inside
a factory process (`CURL_DARK_FACTORY_LANE` set) by design, and the lane's guard would not
let it clear the variable, so the lane could check only `-SelfTest` and a parse under
Windows PowerShell 5.1. These two criteria moved here from BL-1740. Interactive only
(`lane: no`): no lane can run the script.

## Acceptance criteria

- [x] `Gap\RunGapAnalysis.cmd -DryRun` prints the refusal check, then every step's exact command for all seven areas, and changes nothing (`git status` and `git worktree list` are unchanged).
- [x] A real run `Gap\RunGapAnalysis.cmd -Areas exitcodes,writeout -AlongsideShift` on Windows writes both measurements, `run.json` and `gap.log` under `<repo>.gap\<stamp>\`. Its summary is recorded in this task's Notes.

## Notes

- 2026-10-08: proven by two full real runs from an interactive session (all seven areas): 2026-10-08_1640 and 2026-10-08_2029, each writing every measurement, run.json and gap.log under Z:eposCurl.gap<stamp>; 2026-10-08_2029: options 567/573, protocols 27/29, features 18/23, writeout 138/138, exitcodes 259/259, environment 24/42, behaviour 651/1327. -DryRun was used before the first run.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing. Interactive
- 2026-10-08: Doing -> Done. Proven by real runs 2026-10-08_1640 and 2026-10-08_2029
