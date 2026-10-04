# ADR-0406 — A fixed-lane shift retires idle lanes down to the board's capacity

- **Status:** Accepted
- **Date:** 2026-10-03
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

Audit finding AF-0032 (BL-1374): the 2026-10-03 06:12 shift ran a fixed nine lanes for about
seven hours, and its lanes spent 1299 of their 1793 idle minutes waiting with "No task can start
yet: every ready task overlaps one in Doing" - about 38% of lane time, over the audit's 20% bar.
`-Lanes Auto` already caps its lane count with `task-board.ps1 capacity` (ADR-0130), but a
fixed `-Lanes N` shift kept all N lanes running whatever the board could run at once, and every
lane without a task polled the board once a minute until the shift ended. The touches that
serialised the lanes vary from board to board; the waste is the lanes, not any one task.

## Decision

1. A fixed shift of more than one lane takes a capacity step every 5 minutes, except while it
   waits for tokens or its time is up: it reads the board's capacity exactly as `-Lanes Auto`
   does, and when more lanes run than the capacity (at least 1) it retires lanes down to it,
   but no more lanes than hold no task. When the capacity rises again it adds one lane per
   step, never above the N asked for. Each change traces `lanes a -> b (reason)`.
2. Every retire, Auto's included, goes to the highest-numbered lane that holds no task when
   there is one, so the retired lane stops within a minute instead of after a whole task.
3. The pure rule is `Get-CapacityLaneCount` (and `Get-LaneToRetire -Idle`), proved by
   `RunDarkFactory.ps1 -TestAutoLanes`.

## Consequences

- Lanes that cannot start anything stop instead of logging wait lines for hours, so the
  overlap wait the process auditor measures falls with the lane time it wastes.
- A retired lane's tab closes when it retired cleanly; an added lane gets a fresh worktree
  reset to the shift's branch, as an Auto-added one does.
- The logs of shifts run before this change still record the old waits; a re-audit closes
  AF-0032 on a shift run after it.

## Alternatives considered

- **Make fixed shifts Auto.** It would also pace to the token windows, which a fixed count is
  chosen to avoid; only the capacity ceiling was missing.
- **Sleep longer between overlap waits.** The lane is just as idle; it only logs less.
- **Pick tasks whose touches are smaller.** The board already picks every task that can start;
  the overlap is in the tasks filed, not in the picking.
