---
id: BL-4353
title: Restart a shift coordinator that stops writing its log while its lanes run
priority: High
assignee: Claude
pipeline: bugfix
depends-on: []
touches: [DarkFactory/ShiftCoordinator.ps1, DarkFactory/Heartbeat.ps1, RunDarkFactory.ps1]
requirement: none
created: 2026-10-30
completed: 2026-10-30
---
# BL-4353 — Restart a shift coordinator that stops writing its log while its lanes run

## Goal

A coordinator that hangs is noticed and replaced within a few minutes, so CI watching, heartbeats and lane restarts never stop silently while lanes keep working.

## Context

On 2026-10-29 the shift coordinator's log (`<repo>.logs\DarkFactory-<stamp>.log`) has nothing between 17:50 and 18:45 Arizona time, when an interactive `-Restart` killed it; its lanes kept claiming until 18:23. Nothing watched CI in that hour: run 40984137987 failed at about 18:30 and its fix task (BL-4299) was filed only at 18:50 by the restarted coordinator, part of a 139-minute red spell (AF-0475..AF-0481 analysis, ADR-1158). The cause of the hang was not found. Nothing today restarts a coordinator that is alive but not working: the coordinator restarts dead lanes, but no one watches the coordinator.

Start by finding what the coordinator was doing: the heartbeat loop in `DarkFactory/ShiftCoordinator.ps1` and its calls (CI watch `gh` calls, `git push` of `board`, usage probes) are candidates for a call with no timeout. Then add a watchdog: e.g. the coordinator stamps a file each loop, and lanes (which already run independently) or a small watcher restart it with the shift's arguments (as `-Restart` does for the coordinator only) when the stamp is older than a few heartbeats; and give the blocking external calls timeouts.

## Acceptance criteria

- [x] The external calls in the coordinator's loop that can block (gh, git push/fetch, usage reads) run with a timeout, and a timeout is traced instead of hanging the loop.
- [x] A coordinator whose loop has not completed for 3 heartbeat intervals is replaced by a new one with the same arguments, without stopping the lanes; the trace says so. A self-test (`-Test*` in the files touched) proves the stale-stamp decision.
- [x] The script help describes the watchdog.

## Notes

- Decided by Claude (ADR-1159): timeouts via coordinator-only `git`/`gh` shadow functions (180 s, network subcommands only); lanes watch `coordinator.stamp` and replace the coordinator with `-TakeOverShift` + the shift's `-ShiftStamp`, keeping the lanes. Added RunDarkFactory.ps1 to touches (new parameters and help); no other Doing task touches it.
- The hang itself was not found (no log); the unbounded `gh`/`git` network calls in the loop are the candidates, now bounded.
- Proof: `-TestCoordinatorWatchdog` 15 PASS; `-TestHeartbeat`, `-TestRestart`, `-TestCiWatch` exit 0. No C# changed, so no build/quality run needed.

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: 3b8207b9 93aa094b 0d992756.
- 2026-10-30: Doing -> Done. Coordinator gh/git network calls time out; lanes replace a coordinator silent for 3 heartbeats without stopping lanes (ADR-1159)
