---
id: BL-1071
title: Return a parked task to Backlog on the shared branch, so its Doing orphan cannot stop every later shift from starting
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-01
completed:
---
# BL-1071 — Return a parked task to Backlog on the shared branch, so its Doing orphan cannot stop every later shift from starting

## Goal

When a lane parks a task because its push keeps being refused, the task leaves `Doing` on the shared branch (back to `Backlog`, with the parked branch named in its Log), so the next shift's orphan check never refuses to start because of it.

## Context

On 2026-09-30 at 21:34 lane 4 logged `BL-1069 PARKED  push kept being refused; back to Backlog`, but BL-1069 stayed in `Tasks/Doing` on `work/dark-factory`: the move back to Backlog was never pushed, and the work (5ee2709a, d264f8da) lived only on the local branch `factory/BL-1069-lane-4-20260930-140644`. Every shift start after that refused with `task already in Doing and held by no lane: BL-1069` (RunDarkFactory.ps1, the orphan check near line 3435) - at 22:38 and again at 02:57 - so the factory did not run for about four and a half hours and nothing alerted anyone. An interactive session cherry-picked the parked work and restarted the shift at 03:01.

Two gaps: the park path does not make its Doing -> Backlog move reach the shared branch (and its lane log line claims it did), and the orphan check refuses outright instead of acting on an orphan whose lane is gone.

## Acceptance criteria

- [ ] Parking a task pushes its move from `Doing` to `Backlog` (with a Log line naming the parked branch) to the shared branch, retrying like an integration; if that push also fails, the lane's summary says so and the end-of-shift report lists it for Stewart.
- [ ] At shift start, an orphan in `Doing` whose lane worktree has no uncommitted work and whose parked branch exists is returned to `Backlog` with a Log line, instead of refusing; an orphan with uncommitted work in a lane worktree is still adopted as today.
- [ ] A shift that still refuses to start says why in the end-of-shift notice, so a refusal is never silent.
- [ ] A `-Test*` case covers each of the three, and every `-Test*` switch prints no FAIL.

## Notes

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
