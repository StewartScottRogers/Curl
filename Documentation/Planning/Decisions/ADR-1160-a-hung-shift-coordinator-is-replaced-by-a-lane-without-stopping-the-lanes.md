# ADR-1160 — A hung shift coordinator is replaced by a lane, without stopping the lanes

- Status: Accepted
- Date: 2026-10-30
- Task: BL-4353
- Decided by Claude under Stewart's delegation.
- Builds on ADR-1158 (a CI fix goes first).

## Context

On 2026-10-29 the shift coordinator's log had nothing between 17:50 and 18:45 while its lanes
kept claiming. Nothing watched CI in that hour, and a CI fix task was filed 20 minutes late. The
coordinator restarts dead lanes, but nothing watched the coordinator. The hung call was not
found; the coordinator's loop calls `gh`, `git fetch` and `git push` with no timeout.

## Decision

1. In the coordinator process, `gh` calls and `git fetch|push|pull|ls-remote|clone` calls run
   through `Invoke-ExternalWithTimeout` (180 seconds, no input; the process tree is stopped on
   timeout, a `timeout` line is traced and the call exits 124). Local git calls and every call
   in a lane are untouched. This is done by two functions named `git` and `gh` that only the
   coordinator defines (`Enable-ExternalCallTimeouts`), so no call site changes and the many
   self-tests that stand in for `gh` are unaffected.
2. Each pass of the coordinator's loop writes `coordinator.stamp` (pid, time, shift end, script,
   arguments). A lane checks it once a minute from `Write-Heartbeat`. When the stamp is 3
   heartbeat intervals old (`-HeartbeatMinutes`, 3 when publishing is off) and the shift has
   time left, the first lane to create `coordinator.replacing` stops the coordinator and what it
   started, but never a lane, and starts a new coordinator with the same arguments plus
   `-ShiftStamp <stamp> -TakeOverShift` and `-Hours` set to the time left. A replacement that
   does not stamp within 10 minutes is replaced again.
3. `-TakeOverShift` makes a coordinator keep the shift's running lanes (a pid file alive, no
   summary) instead of starting any, and skip the check that refuses to start beside a running
   lane. It appends to the same shift trace log.

## Why

Lanes already run independently and already write a heartbeat every minute, so they are the
watcher that needs no new process. Restarting through `-Restart` would have stopped the lanes
and lost their runs; taking the shift over by stamp keeps them. A timeout in the coordinator
only, because a lane's `git push` under the integrate lock may legitimately be slow.

## Consequences

The old coordinator's herdr tab stays open at a prompt; the lanes' tabs are not known to the
new coordinator, so it does not close them or relabel them at shift end. `-TestCoordinatorWatchdog`
proves the decision, the stamp, the processes stopped and the timeout.

## Amendment (BL-4394)

Decided by Claude under Stewart's delegation. A failed stamp write is traced once
(`coordinator.stamp not written`), and clears when a write works again. The stamp is refreshed
before and after every timed gh or git call (`Invoke-TimedExternalCall`) rather than the
threshold being raised: one pass can make four or more calls of up to 180 s each, so any fixed
threshold would either miss a real hang or fire during a GitHub slowdown, while a refresh keeps
the stamp younger than one call (180 s) plus local work. Lanes are not given `-HeartbeatMinutes`
(it would change what a lane publishes); the stamp carries a `heartbeat=` line and the lane
reads its threshold from there, so the 3-interval threshold does follow the coordinator's `-HeartbeatMinutes`.
