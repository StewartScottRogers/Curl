---
id: BL-4369
title: Fix CI failure ConnectAsync_WithAnOpenerOnAClosedPort_AsksOnceTellsOnceThatTheSocketClosedAndFailsWithExit7 on macOS and Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-30
completed:
---
# BL-4369 — Fix CI failure ConnectAsync_WithAnOpenerOnAClosedPort_AsksOnceTellsOnceThatTheSocketClosedAndFailsWithExit7 on macOS and Windows

## Goal

`ConnectAsync_WithAnOpenerOnAClosedPort_AsksOnceTellsOnceThatTheSocketClosedAndFailsWithExit7` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `ConnectAsync_WithAnOpenerOnAClosedPort_AsksOnceTellsOnceThatTheSocketClosedAndFailsWithExit7` failed on macOS and Windows in CI run 41128846186 (https://github.com/StewartScottRogers/Curl/actions/runs/41128846186). First failing commit: aa0b1331.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 41128846186 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `ConnectAsync_WithAnOpenerOnAClosedPort_AsksOnceTellsOnceThatTheSocketClosedAndFailsWithExit7` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-30: Created.
