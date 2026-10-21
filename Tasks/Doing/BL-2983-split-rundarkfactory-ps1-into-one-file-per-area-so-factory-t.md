---
id: BL-2983
title: Split RunDarkFactory.ps1 into one file per area so factory tasks can run in parallel
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1, DarkFactory, CLAUDE.md, Curl.slnx]
lane: no
requirement: none
created: 2026-10-21
completed:
---
# BL-2983 — Split RunDarkFactory.ps1 into one file per area so factory tasks can run in parallel

## Goal

Factory tasks that change different areas of the dark factory (claims, integration, CI watch, lane
pacing, heartbeats, ...) touch different files, so the board can run them side by side.

## Context

Stewart, 2026-10-21, approved splitting the script. `task-board.ps1 hotspots` listed
`RunDarkFactory.ps1` with 7 waiting tasks (BL-2945 to BL-2951), and the script is 6,237 lines with
188 functions and 22 self-test blocks. `RunDarkFactory.ps1` stays the entry point (its command lines
are matched by `Audit/RunAudit.ps1` and named in CLAUDE.md, memory and lane prompts); the parts live
in `DarkFactory/` and are dot-sourced in their original order, so behaviour is unchanged.

## Acceptance criteria

- [ ] `RunDarkFactory.ps1` keeps the header help, the param block and the entry, and dot-sources the
      area files in `DarkFactory/`; no area file is more than about 800 lines, each holds one area's
      functions with that area's self-test block, and each is ASCII only.
- [ ] Every self-test switch passes: -TestLaneMarker, -TestAlarm, -TestAutoLanes, -TestMachineProbe,
      -TestOutOfTokens, -TestTaskIds, -TestModelChoice, -TestTaskBudget, -TestAffectedTests,
      -TestFlakyTests, -TestShiftBranch, -TestAuditCadence, -TestAudioOff, -TestCiWatch,
      -TestTaskStash, -TestToolLabels, -TestPark, -TestClaimDuplicates, -TestIntegrateLock,
      -TestHeartbeat, -TestRestart; and -AutoLanesReport prints its reading.
- [ ] The waiting factory tasks (BL-2945 to BL-2951) name in `touches` the area files their work
      lands in, edited in Backlog by the task board's rules.
- [ ] `Audit/Tools/Test-AuditDue.ps1` counts a change under `DarkFactory/` as a factory-script
      change, through a pull request from `master` (an audit path).
- [ ] CLAUDE.md says where the factory's code lives.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Interactive session; Stewart approved 2026-10-21. Criteria at claim: 311cb162 31ad115a 1c2294ce 0bc8d395 a2fb7b1b.
