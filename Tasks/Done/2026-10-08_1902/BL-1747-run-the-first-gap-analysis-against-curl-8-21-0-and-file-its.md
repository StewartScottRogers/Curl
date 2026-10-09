---
id: BL-1747
title: Run the first gap analysis against curl 8.21.0 and file its tasks
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1746]
touches: [Gap/Findings, Gap/Scorecards, Gap/Upstream, Gap/Baselines, Tasks/Backlog]
lane: no
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1747 — Run the first gap analysis against curl 8.21.0 and file its tasks

## Goal

The gap analysis office has measured Curl against curl 8.21.0 in all seven areas and filed
the first gap findings and scorecard on the `gap` branch. It has merged them to `master`
through a green pull request, filed the resulting Curl tasks on the board, and published the
dashboard with real data.

## Context

This is ADR-0433, end to end. Interactive only (`lane: no`): it writes `Gap/`, which BL-1746
made an audit path. Run it with `/task-run BL-1747` in an interactive session, between
shifts or with `-AlongsideShift`. Read `Gap/README.md` (BL-1745) first.

**Steps.**

1. Confirm `Gap/` has reached `master`: the tools, agents and inventories built on
   `work/dark-factory` arrive there through a shift-end merge. When `origin/master` lacks
   `Gap/RunGapAnalysis.ps1`, the `gap` branch cannot run. Wait for the merge, or note why
   not and stop with the task in `Blocked`.
2. `Gap\RunGapAnalysis.cmd -DryRun`, then `Gap\RunGapAnalysis.cmd -NewTab` (add
   `-AlongsideShift` if a shift is running). Watch `<repo>.gap\<stamp>\gap.log`.
3. Read the scorecard. Sanity-check it against known figures. The behaviour area's `match`
   count should be close to the number of cases listed in
   `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` (559 on 2026-10-07, ADR-0420),
   and the per-reason unmeasured table should add up. When a number is plainly wrong, file
   an interactive-only task against the tool at fault, fix it before going on, and rerun.
4. Merge the `gap` to `master` pull request once CI is green on all three platforms, then
   merge `master` into `work/dark-factory`.
5. Check that the filed tasks are on the board (`task-board.ps1 status`), that none says
   `lane: no` or touches a `Gap/` path, and that each carries its finding's evidence and
   suggestion.
6. Run the release watcher by hand: `gh workflow run gap-release-watch.yml`. With curl
   8.22.0 out, it should record `scope: newest` findings tagged 8.22.0, and the dashboard
   should show "curl 8.22.0 released: N new gaps".
7. Open `https://stewartscottrogers.github.io/Curl/gaps/` and check it shows this run.

## Acceptance criteria

- [x] `Gap/Scorecards/` on `master` holds the first scorecard, with all seven areas measured on Windows, and `Gap/Scorecards/history.json` holds its entry.
- [x] `Gap/Findings/` on `master` holds the run's findings, each with evidence, `introduced-in`, severity and suggestion.
- [x] Every open target-scope finding names a task in `task`, and each such task is in `Tasks/Backlog` with no `lane: no`.
- [x] The release watcher's run recorded the 8.22.0 release gaps, and the dashboard shows the run's scores and the release banner.
- [x] This task's Notes record each area's X of Y, the overall score, the number of findings and tasks filed, and the scorecard's file name, so Stewart can read the result here.

## Notes

- 2026-10-08: First gap analysis, run 2026-10-08_1640, Windows, target curl 8.21.0, commit 0fcab815. Scorecard `Gap/Scorecards/2026-10-08_1640.md`, merged to master in PR #80 (CI green on all three platforms); master merged into work/dark-factory.
  - options 567 of 573 (99.0%), protocols 27 of 29 (93.1%), features 16 of 23 (69.6%), writeout 138 of 138 (100%), exitcodes 259 of 259 (100%), environment 24 of 42 (57.1%; 18 unmeasured), behaviour 578 of 1326 (43.6%; 605 unmeasured, 687 excluded). Overall 1609 of 2390 (67.3%).
  - Behaviour sanity check: 561 harness passes against `PassingUpstreamCases.txt`'s 559, then 578 matches after the reference cross-check.
  - 37 target findings, 37 lane-eligible tasks filed (none `lane: no`, none touching `Gap/`). The release watcher (run 37865797629) recorded curl 8.22.0 with 3 new gaps (`scope: newest`). The dashboard https://stewartscottrogers.github.io/Curl/gaps/ shows this run and newest 8.22.0.
  - Fixed on the way, each through a green audit PR: the probe ignored `-Candidate` and environment recipes could not run (PR #79); options and writeout stamped the tool repository's commit (PR #81, this run's two stamps corrected by hand before its scorecard was written).
  - Follow-ups: BL-1839 (79 unknown-variable cases), BL-1840 (cross-check wrote `%` into the main checkout and stopped a shift starting), BL-1841 (the options analyst wrote into the tree, so options filed no findings this run).

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Blocked. First gap analysis running (herdr tab); finished interactively once its gap pull request merges, so Doing holds nothing orphaned
- 2026-10-08: Blocked -> Doing.
- 2026-10-08: Doing -> Done. First gap analysis merged in PR #80 with CI green; dashboard live
