# ADR-0758 — An idle -Continuous shift polls the board until work is claimable

- Status: Accepted
- Date: 2026-10-22
- Task: BL-3092
- Decided by Claude under Stewart's delegation.

## Context

The 2026-10-22 02:13 `-Continuous` shift ended "nothing ready" and exited. Work became
claimable at about 02:40 (BL-3011 landed and 30 docs tasks were filed), but no shift started
until a manual `-Restart` at 02:44. ADR-0757 made the shift end count only tasks claimable
now, so a board holding only cost-capped tasks also ended the shift, with nothing to notice
when the cap freed.

## Decision

- A `-Continuous` coordinator that finds nothing claimable at shift end, after the
  shift-end review (ADR-0710) has run, does not exit. Every `-IdlePollMinutes` (new
  `RunDarkFactory.ps1` parameter, 1 to 60, default 5, forwarded by the hand-over) it pulls
  the shift branch with `--ff-only` and asks `Get-ClaimableTaskId` (ADR-0757) again.
- Each look is decided by `Get-IdlePollDecision` in `DarkFactory/ShiftCoordinator.ps1`, a
  pure function of the clock, the shift's `-Hours` deadline, `Get-UsageStop`'s reason and the
  claimable and cost-capped IDs: `stop` past the deadline or while a usage limit
  (`-StopAtUsage`, `-StopAtWeeklyUsage`) says no shift may start, `start` when a task is
  claimable, else `wait`, also when only cost-capped tasks are ready.
  `RunDarkFactory.ps1 -TestHeartbeat` drives it.
- `start` uses the one hand-over the shift end already had (`$startNextShift`, which calls
  `Start-Detached`). The argument list exists once.
- While it polls, the coordinator writes a lane 0 heartbeat with phase `idle` and step
  `polling` (`Write-IdlePollHeartbeat`) and refreshes it every minute.
  `Write-Heartbeat` publishes it on the board branch every `-HeartbeatMinutes`. When polling
  stops, it writes `finished` and publishes once more with state `ended`.
- What waits on Stewart (`$reasons`) is traced once before polling, and the tab label says
  "ALARM, read; idle, polling". `Invoke-Alarm` waits for a key press in a console, so ringing
  it before polling would hold the poll until Stewart came back. It rings only when polling
  stops, as a shift without `-Continuous` rings it at its end.
- A shift without `-Continuous` ends as before: the alarm when something waits on Stewart,
  else it closes its tab.

## Consequences

- An idle factory starts the next shift within `-IdlePollMinutes` of work becoming claimable,
  including when a cost cap frees, without anyone restarting it.
- Polling ends at the shift's own `-Hours` deadline. After that, starting again takes a new
  shift, as before.
- Each look costs one `git pull` and a few `task-board.ps1 next` calls. No Claude tokens are
  spent while idle.

## Amendment (BL-3142): the poll also runs during the merge wait

The shift-end merge waits up to `$MergeCiWaitMinutes` for CI before the idle poll is reached,
so work filed meanwhile waited unclaimed (shift 20261022-140915: 25 tasks, about 11 minutes
of CI wait turned into 21 minutes idle). Under `-Continuous` the same poll now also runs every
`-IdlePollMinutes` during that wait (`Wait-MergeCiRun`, `-OnPoll`). When a task is claimable,
the coordinator starts the next shift through the one hand-over (`$startNextShift -Beside`),
exactly once, then lets the merge finish and runs the review beside the next shift, as ADR-0710
does. Decided by Claude under Stewart's delegation.
