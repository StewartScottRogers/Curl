---
id: BL-895
title: Restart a running dark factory shift with -Restart, stopping each lane as soon as it is safe
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1, CLAUDE.md]
requirement: none
created: 2026-09-29
completed:
---
# BL-895 — Restart a running dark factory shift with -Restart, stopping each lane as soon as it is safe

## Goal

`RunDarkFactory.cmd -Restart` stops this checkout's running shift and starts a new one
with the same arguments. It stops the coordinator first, then each lane as soon as that
lane is neither claiming nor integrating, so a restart takes minutes rather than waiting
for every lane to be quiet at once.

## Context

2026-09-28/29: Claude restarted shifts by hand, killing every Curl factory process once
no lane's heartbeat said `integrate` or `claim`. With six lanes one almost always does,
and the restart asked for at about 23:10 fired at 02:37. Stewart: "fix the restart wait".
A lane must not be stopped mid-claim, or it can leave a task in Doing that no lane holds,
which stops the next shift from starting. It must not be stopped mid-integration either,
or it can leave a half-finished rebase. The coordinator must go first, or it restarts
each lane it sees die.

## Acceptance criteria

- [ ] `-Restart` finds the coordinator whose command line runs this checkout's
      `RunDarkFactory.ps1` without `-Lane` (never another repository's factory). With no
      coordinator it says so and exits 1.
- [ ] It stops the coordinator's process tree, then, every 5 seconds, stops each live
      lane of the newest `lanes-<stamp>` whose heartbeat phase is not `claim` or
      `integrate`, tracing each, until none is left.
- [ ] It starts the new shift with `Start-Detached`, passing the old coordinator's
      arguments, and prints where.
- [ ] A pure `Select-LanesToStop` decides which lanes may stop from their phases, and
      `-TestRestart` proves it (run, tokens, wait, finished and a missing heartbeat stop;
      claim and integrate wait).
- [ ] The script header and `CLAUDE.md` say to restart a shift with `-Restart`.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
