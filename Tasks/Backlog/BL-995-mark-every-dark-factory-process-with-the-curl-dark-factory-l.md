---
id: BL-995
title: Mark every dark factory process with the CURL_DARK_FACTORY_LANE environment variable
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-993, BL-994]
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-09-29
completed:
---
# BL-995 — Mark every dark factory process with the CURL_DARK_FACTORY_LANE environment variable

## Goal

Every process a dark factory shift runs work in - each lane, the coordinator, a single-lane shift, and every `claude -p` and `task-board.ps1` they start - has `CURL_DARK_FACTORY_LANE` set, so the audit guards (BL-996, BL-997) can tell the factory from an interactive session.

## Context

Interactive only (`lane: no`): this is audit guard layer 2's marker (the ADR from BL-994),
and the factory does not build its own guards. Run it with `/task-run BL-995`.

Nothing marks a lane today: `RunDarkFactory.ps1` sets only `CLAUDE_PROJECT_DIR` (line
~324) in its own environment and `BASH_DEFAULT_TIMEOUT_MS`/`BASH_MAX_TIMEOUT_MS` on the
`claude -p` process (`Invoke-TaskRun`, line ~2341). Children started with
`System.Diagnostics.ProcessStartInfo` inherit the parent's environment, and
`Invoke-Board` runs `task-board.ps1` as a child, so setting the variable once in the
process is enough.

Design:

- Right after the `-NewTab` hand-off and the `-Restart` handling (processes that only
  launch another shift and exit are not marked), set
  `$env:CURL_DARK_FACTORY_LANE` to the lane number when `-Lane N` is given, otherwise to
  `0` (the coordinator, or a shift that runs tasks itself).
- Self-test switches (`-Test*`) do not set it, so running a self-test from an interactive
  session never marks that session's children.
- Trace it once at start, e.g. `shift start ... lane-marker=CURL_DARK_FACTORY_LANE=3`.
- A lane cannot unmark itself: hooks are started by the Claude Code process, whose
  environment the lane's Bash tool cannot change. Say so in the script header.

## Acceptance criteria

- [ ] A new switch `-TestLaneMarker` prints `PASS` lines and no `FAIL` line showing that the marker function returns `3` for `-Lane 3`, `0` for a coordinator or single-lane shift, and nothing for a `-NewTab` or `-Restart` launcher.
- [ ] A `claude -p` started by `Invoke-TaskRun` sees the variable: proven in the self-test by starting `cmd /d /c echo %CURL_DARK_FACTORY_LANE%` through the same `ProcessStartInfo` construction and reading `3` back.
- [ ] The shift trace's `start` line names the marker's value.
- [ ] The script header describes `CURL_DARK_FACTORY_LANE`: who sets it, its values, and that BL-996 and BL-997 read it.
- [ ] Every existing `-Test*` switch still prints no `FAIL` line.

## Notes

## Log

- 2026-09-29: Created.
