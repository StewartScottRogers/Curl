---
id: BL-792
title: Clear a lane's herdr tab after each task and caption it empty while it holds none
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-09-28
completed:
---
# BL-792 — Clear a lane's herdr tab after each task and caption it empty while it holds none

## Goal

A dark factory lane's herdr tab shows only the task it is running: once a task is
finished and integrated, the tab's screen is cleared, and while the lane holds no task
its caption and screen say `empty`.

## Context

Stewart, 2026-09-28: "make sure the herdr tabs are cleared once the task the run is
completed. If no new task then state in tab empty." Today a lane's tab keeps the finished
run's output and its caption keeps the finished task's ID and title until the next claim
renames it, so a lane between tasks, or waiting because every ready task overlaps work in
`Doing`, looks as if it is still running the old task. The tab caption convention is
`DF <shift HH:mm> L<n> · <text>`, where a finished tab ends in `close` or `read`
(`Get-LaneTabLabel`, `Set-OwnTabLabel` in `RunDarkFactory.ps1`).

## Acceptance criteria

- [ ] After a lane's task is integrated (Done, Blocked, Backlog or parked), the lane
      clears its screen and prints one line naming the task just finished and its result,
      e.g. `BL-514 done at 16:10; empty, waiting for the next task`.
- [ ] While the lane holds no task, its caption is `DF <HH:mm> L<n> · empty`: set before
      each claim and kept while the claim waits on overlapping work.
- [ ] A lane that ends cleanly (nothing ready, retired, time up or tokens low, with no
      stall and no block) clears its screen, prints why it ended, and captions itself
      `DF <HH:mm> L<n> · empty, close`. A lane that blocked or stalled a task keeps its
      output for Stewart, as today.
- [ ] Only lanes change; the single-runner shift (`-Lanes 1`) and the coordinator's tab
      behave as before.
- [ ] `powershell -NoProfile -File RunDarkFactory.ps1 -TestAutoLanes` still passes, the
      script parses, and the header documents the empty caption.

## Notes

Pipeline `direct`: a script change, then the build and fast tests. Lanes that are
already running keep the old behaviour until they restart or the next shift starts.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
