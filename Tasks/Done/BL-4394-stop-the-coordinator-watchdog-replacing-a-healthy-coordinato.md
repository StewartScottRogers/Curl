---
id: BL-4394
title: Stop the coordinator watchdog replacing a healthy coordinator on a failed stamp write or slow network
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/Heartbeat.ps1, DarkFactory/ShiftCoordinator.ps1]
requirement: none
created: 2026-10-31
completed: 2026-10-31
---
# BL-4394 — Stop the coordinator watchdog replacing a healthy coordinator on a failed stamp write or slow network

## Goal

The coordinator watchdog replaces a coordinator only when it is really hung: a failed stamp write is traced, and a pass slowed by network timeouts never crosses the threshold.

## Context

Shift-end review 2026-10-31 (silent-failure-hunter, factory code-reviewer) of BL-4353 / ADR-1160. DarkFactory/Heartbeat.ps1:310-326 Write-CoordinatorStamp ends in `catch { }`: once writes start failing nothing is logged, and within 9 minutes lanes kill a healthy coordinator, possibly mid push; the replacement fails the same way. Heartbeat.ps1:341-353 fixes the threshold at 3 heartbeats (9 minutes) while one loop pass (ShiftCoordinator.ps1:534-608) can make four or more timed git/gh calls of up to 180 s each (Heartbeat.ps1:200), so during a GitHub hang lanes replace coordinators that are only waiting. The threshold also ignores -HeartbeatMinutes because $laneArgsFor (ShiftCoordinator.ps1:289) does not forward it, though RunDarkFactory.ps1's help and ADR-1160 say it follows it.

## Acceptance criteria

- [x] Write-CoordinatorStamp writes a trace line on its first failure, shown by a factory -Test* self-test.
- [x] The stamp is refreshed between timed external calls (for example from Invoke-TimedExternalCall), or the threshold exceeds the worst-case pass, and ADR-1160 states which and why.
- [x] Lanes receive -HeartbeatMinutes, or the help and ADR-1160 stop saying the threshold follows it.
- [x] The factory's -Test* self-tests pass under Windows PowerShell and pwsh.

## Notes

Stamp now carries heartbeat=; lanes read it (no -HeartbeatMinutes forwarding, which would change lane publishing). Stamp refreshed around each timed call. ShiftCoordinator.ps1 added to touches for the self-test; no other Doing task held it. ADR-1160 amended. -TestCoordinatorWatchdog passes under pwsh and Windows PowerShell.

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: 74dd1568 e03315f3 d7470d17 caed2003.
- 2026-10-31: Doing -> Done. Stamp write failure traced, stamp refreshed around timed calls, lanes read heartbeat from stamp
