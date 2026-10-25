---
id: BL-3509
title: Find why the 05:02 continuous handover's next shift died with no log
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1, DarkFactory/HerdrTabs.ps1, DarkFactory/ShiftCoordinator.ps1]
requirement: none
created: 2026-10-25
completed: 2026-10-25
---
# BL-3509 — Find why the 05:02 continuous handover's next shift died with no log

## Goal

A continuous shift's handover either starts the next shift, which writes its log, or leaves an alarm saying why it could not.

## Context

At 05:02:31 Arizona on 2026-10-25 the shift `DarkFactory-20261025-035926.log` traced "work is still ready; next shift started (herdr tab w4:t10)" and closed its own tab. No `DarkFactory-*.log` was written after it, no `RunDarkFactory.ps1` process was left, and tab w4:t10 was gone by 07:45, with no alarm, so no shift ran for 2 hours 44 minutes. BL-3507 and BL-3508 were ready the whole time; an interactive session started a shift at 07:46 and it claimed BL-3507 at once. No task was in Doing at 05:02, so the "Doing and held by no lane" refusal from 2026-10-24 20:34 is not the cause. Start where the handover launches the next shift (the `-Continuous` path in `RunDarkFactory.ps1`) and where herdr tabs are opened and closed (`DarkFactory/HerdrTabs.ps1`): whether the new tab's process is started before the old tab closes, and whether closing the old tab can kill the child it launched.

## Acceptance criteria

- [x] The task's Notes name the cause of the 05:02 failure, with the evidence (trace lines, herdr output or a reproduction).
- [x] A handover whose next shift does not write its log within 2 minutes raises the alarm, naming the tab.
- [x] A test or the script's self-test covers the cause, and passes.

## Notes

Cause (most probable; the dead tab left no output to reproduce from): the 05:02:31 trace line "next shift started (herdr tab w4:t10)" only proves `herdr tab create` returned a tab id. `Start-Detached` then discarded the exit code and output of `herdr pane run` (and a 15 s `wait-output` timeout was ignored too), so a command that herdr refused or that the new shell dropped before its prompt was ready left an empty tab, counted as a started shift. The tab then closed or was lost with no `DarkFactory-*.log` ever written, and the old shift had already closed its own tab, so nothing alarmed. Evidence: the log DarkFactory-20261025-035926.log lines 17-19 (started, closed finished tabs, closed own tab at 05:02:31) and no later log.
Fix: `Start-Detached` retries a failed `pane run` once and traces each failure; new `Wait-NextShiftLog` (HerdrTabs.ps1) waits up to 120 s for another DarkFactory-*.log, else raises the alarm naming the tab and the coordinator keeps its own tab (ShiftCoordinator.ps1, added to touches; no other Doing task holds it). Self-test: `RunDarkFactory.ps1 -TestLaneMarker`, 2 new cases, all 6 pass.
## Log

- 2026-10-25: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: 806b5e6a 46fbb42b 59bb2ce2.
- 2026-10-25: Doing -> Done. handover now alarms when the next shift writes no log in 2 minutes and retries a failed pane run
