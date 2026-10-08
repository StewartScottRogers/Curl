---
id: BL-1793
title: Run Gap\RunGapAnalysis.cmd -Areas exitcodes -NoTasks -NoCommit end to end from an interactive session and record the outcome
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1741]
touches: [Gap/RunGapAnalysis.ps1]
lane: no
requirement: none
created: 2026-10-08
completed:
---
# BL-1793 — Run Gap\RunGapAnalysis.cmd -Areas exitcodes -NoTasks -NoCommit end to end from an interactive session and record the outcome

## Goal

A real gap run of one area, `Gap\RunGapAnalysis.cmd -Areas exitcodes -NoTasks -NoCommit -AlongsideShift`, is shown to write its report, findings, scorecard, history entry and dashboard data without committing anything.

## Context

BL-1741 added steps 7 to 11 (analysts, findings, scorecard, pull request, tasks) to `Gap/RunGapAnalysis.ps1` and checked them with `-SelfTest` and `-DryRun`. Its real-run criterion could not be met in a dark factory lane: the script refuses to run under `CURL_DARK_FACTORY_LANE` by design (step 1), and the run starts a headless `claude` analyst. Run it from an interactive session. The gap worktree is `<repo>.gap\gap-branch`; if no `gap` branch exists, the run creates a local `gap` branch from `origin/master`.

## Acceptance criteria

- [ ] `Gap\RunGapAnalysis.cmd -Areas exitcodes -NoTasks -NoCommit -AlongsideShift` writes `<stamp>\reports\gap-exitcodes.md`, at least one finding or a scorecard showing none, `Gap/Scorecards/history.json` with one new entry, and `<stamp>\data.json`. It commits nothing, pushes nothing and opens no pull request.
- [ ] Afterwards the gap worktree's changes are discarded with `git -C <gap worktree> checkout -- . ; git -C <gap worktree> clean -fd Gap`, and the outcome (and any fix the run needed, as its own task) is recorded in this task's Notes.

## Notes

## Log

- 2026-10-08: Created.
