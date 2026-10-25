# ADR-0813 — A lane ends when only interactive work is left

- **Status:** Accepted
- **Date:** 2026-10-25
- **Amends:** the lane's "nothing ready" wait in `DarkFactory/Claim.ps1` (`Invoke-Claim`), and BL-2069's rule for what counts as interactive only in Doing

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-3500.

## Context

AF-0435 found the shift of 2026-10-24 18:51 idle for 85 of its 215 lane-minutes (39.5%,
against the process auditor's 20% limit). From 19:13 to 20:17, lane 1 logged "No task is
ready. Backlog is interactive only, waiting on Stewart or an interactive session" about once
a minute. Lanes 2 and 3 logged it for about 13 minutes each before `-Lanes Auto` retired
them. Every Backlog task was `lane: no` or touched a guard path.

A lane with nothing ready waited whenever any task was in Doing, on the reasoning that the
task's end might make more work ready. The only task in Doing was BL-3465, which is
`lane: no` and was held by an interactive session. The lane waited until that session moved
it to Done at 20:17:34. It then ended the shift with "nothing ready" at 20:18:35, which it
could have done an hour sooner.

The same evidence shows a second gap. `Test-InteractiveOnlyTask`, which shift start uses to
leave an interactive session's task in Doing alone (BL-2069), read only `lane: no`. The board
treats a task that touches an audit path or guard file as interactive only whatever its
`lane:` line says. BL-3472 touched `.claude` and had no `lane: no`, so while an interactive
session held it in Doing, the next shift refused to start twice ("BL-3472 is in Doing and
held by no lane").

## Decision

1. When `next` offers nothing and says "No task is ready", a lane waits only while a
   **lane-eligible** task is in Doing. Interactive-only tasks in Doing do not count: only a
   session finishes them, in its own time. With none of the lane-eligible kind, the lane
   ends at once with `None`, the same way it ends when Doing is empty. A "No task can start
   yet" answer (ready work overlapping Doing) still waits, as before.
2. When it ends this way with interactive work left, the lane writes one `idle` trace line
   naming that work: the interactive tasks in Doing and `Get-InteractiveHoldNote`'s Backlog
   sentence. The line's verb is `idle`, not `wait`, so an interactive session can see what to
   take without the lane spending any wait time.
3. `Test-InteractiveOnlyTask` follows the board's rule: `lane: no`, or any `touches` entry
   that is an audit path or guard file. It does not copy the list of those paths. It parses
   `task-board.ps1`'s own `Test-AuditPath` function out of that script, once per process,
   because CI's audit guard self-test keeps that list complete (BL-1209). The function
   moved from `DarkFactory/Park.ps1` to `DarkFactory/Claim.ps1`, which loads before every
   caller (the claim, shift start in `ShiftRunner.ps1` and `ShiftCoordinator.ps1`).

The change to the wait does not end the shift any sooner than lanes already allow.
`Invoke-Claim` returns `None` only when no lane-eligible task is in Doing, and a lane that is
running or integrating a task keeps that task in Doing on the shared branch. So every lane
reaches the same answer together, and none ends while another lane's work could still unlock
something.

## Why not a grace period

A wait of a few minutes before ending would only add idle time. The hand-over already covers
work that becomes ready later. A `-Continuous` shift that ends with nothing claimable polls
the board every `-IdlePollMinutes` until its `-Hours` deadline (BL-3092, ADR-0758). The
shift-end merge's CI wait polls it too (BL-3142). The poll starts the next shift as soon as a
task is claimable, including one that an interactive session's Done unlocks or files. The
coordinator does that polling, and it writes no lane log, so the process auditor does not
count it as lane time.

## Consequences

- `Audit/Tools/Measure-FactoryProcess.ps1` counts `waitNothingReadyMinutes` from lane `wait`
  lines that begin "No task is ready". With this change, a board whose only work is
  interactive writes no such line. AF-0435's 64 minutes on lane 1 would have been about 0,
  and lanes 2 and 3 would have waited only while lane-eligible work was still in Doing.
- Shift start no longer refuses over an interactive session's guard-path task in Doing.
- The shift ends sooner when only interactive work is left. Its end-of-shift merge, review
  and audit-due check then run sooner too. Without `-Continuous`, a later lane-eligible task
  waits for the next shift that someone starts, as it did before whenever Doing was empty.
- Ready work that overlaps an interactive task in Doing ("No task can start yet") still
  waits. The process auditor counts that wait as overlap, not nothing-ready. It is left as
  it is until a finding measures it.
