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
completed: 2026-10-01
---
# BL-1071 — Return a parked task to Backlog on the shared branch, so its Doing orphan cannot stop every later shift from starting

## Goal

When a lane parks a task because its push keeps being refused, the task leaves `Doing` on the shared branch (back to `Backlog`, with the parked branch named in its Log), so the next shift's orphan check never refuses to start because of it.

## Context

On 2026-09-30 at 21:34 lane 4 logged `BL-1069 PARKED  push kept being refused; back to Backlog`, but BL-1069 stayed in `Tasks/Doing` on `work/dark-factory`: the move back to Backlog was never pushed, and the work (5ee2709a, d264f8da) lived only on the local branch `factory/BL-1069-lane-4-20260930-140644`. Every shift start after that refused with `task already in Doing and held by no lane: BL-1069` (RunDarkFactory.ps1, the orphan check near line 3435) - at 22:38 and again at 02:57 - so the factory did not run for about four and a half hours and nothing alerted anyone. An interactive session cherry-picked the parked work and restarted the shift at 03:01.

Two gaps: the park path does not make its Doing -> Backlog move reach the shared branch (and its lane log line claims it did), and the orphan check refuses outright instead of acting on an orphan whose lane is gone.

## Acceptance criteria

- [x] Parking a task pushes its move from `Doing` to `Backlog` (with a Log line naming the parked branch) to the shared branch, retrying like an integration; if that push also fails, the lane's summary says so and the end-of-shift report lists it for Stewart.
- [x] At shift start, an orphan in `Doing` whose lane worktree has no uncommitted work and whose parked branch exists is returned to `Backlog` with a Log line, instead of refusing; an orphan with uncommitted work in a lane worktree is still adopted as today.
- [x] A shift that still refuses to start says why in the end-of-shift notice, so a refusal is never silent.
- [x] A `-Test*` case covers each of the three, and every `-Test*` switch prints no FAIL.

## Notes

- Cause found in lane 4's trace: at 21:33-21:38 every `git fetch`/`git push` failed with `unable to access 'https://github.com/...'` - the network was out, so the park's three back-to-back tries all failed, and the lane still traced "back to Backlog".
- `Invoke-Park` now retries the move for about nine minutes (waits 10, 30, 60, 120, 300 s, `-RetrySeconds`), commits it as `chore(tasks): park <ID>` (was `block`), and returns `<ID> PARK NOT PUSHED ...` when it never lands. The lane traces that in red, puts it in its summary after the stall lines (so the coordinator's end-of-shift report and alarm list it) and its tab reads `STALLED, read`.
- Shift start, for a task in Doing that no previous-shift lane holds (`Get-OrphanAction`): exactly one lane worktree with uncommitted work, not already adopted -> that lane adopts it; several such worktrees -> refuse (which work is whose cannot be told); none, with a parked `factory/<ID>-lane-*` branch local or on origin -> moved to Backlog with a Log line naming the branch, committed and pushed (a failed push resets the checkout to origin and refuses); otherwise refuse. Choice: dirt cannot be attributed to a task, so one dirty worktree is taken as the orphan's work; that is the least surprising reading and the adopted lane resumes it rather than losing it.
- Every coordinator and single-runner start refusal now goes through `Stop-ShiftStart`, which traces it and raises the alarm (the end-of-shift notice) with `SHIFT REFUSED  <why>`. The audit-running refusal is left as it was: it is a deliberate wait, not a stop.
- `-TestPark` proves all three on a throwaway repository with a bare origin (6 cases). Every `-Test*` switch run: no FAIL (TestAlarm and TestOutOfTokens are rehearsals, not checks, and were not run).

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. A park that cannot push says so in the lane summary and shift report, and shift start adopts or requeues a Doing orphan instead of silently refusing
