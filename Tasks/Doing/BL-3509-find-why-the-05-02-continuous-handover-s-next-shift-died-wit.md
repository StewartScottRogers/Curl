---
id: BL-3509
title: Find why the 05:02 continuous handover's next shift died with no log
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1, DarkFactory/HerdrTabs.ps1]
requirement: none
created: 2026-10-25
completed:
---
# BL-3509 — Find why the 05:02 continuous handover's next shift died with no log

## Goal

A continuous shift's handover either starts the next shift, which writes its log, or leaves an alarm saying why it could not.

## Context

At 05:02:31 Arizona on 2026-10-25 the shift `DarkFactory-20261025-035926.log` traced "work is still ready; next shift started (herdr tab w4:t10)" and closed its own tab. No `DarkFactory-*.log` was written after it, no `RunDarkFactory.ps1` process was left, and tab w4:t10 was gone by 07:45, with no alarm, so no shift ran for 2 hours 44 minutes. BL-3507 and BL-3508 were ready the whole time; an interactive session started a shift at 07:46 and it claimed BL-3507 at once. No task was in Doing at 05:02, so the "Doing and held by no lane" refusal from 2026-10-24 20:34 is not the cause. Start where the handover launches the next shift (the `-Continuous` path in `RunDarkFactory.ps1`) and where herdr tabs are opened and closed (`DarkFactory/HerdrTabs.ps1`): whether the new tab's process is started before the old tab closes, and whether closing the old tab can kill the child it launched.

## Acceptance criteria

- [ ] The task's Notes name the cause of the 05:02 failure, with the evidence (trace lines, herdr output or a reproduction).
- [ ] A handover whose next shift does not write its log within 2 minutes raises the alarm, naming the tab.
- [ ] A test or the script's self-test covers the cause, and passes.

## Notes

## Log

- 2026-10-25: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: 806b5e6a 46fbb42b 59bb2ce2.
