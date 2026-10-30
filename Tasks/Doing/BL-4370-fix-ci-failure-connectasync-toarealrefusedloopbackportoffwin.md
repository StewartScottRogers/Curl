---
id: BL-4370
title: Fix CI failure ConnectAsync_ToARealRefusedLoopbackPortOffWindows_FailsWithExit7InUnder500Milliseconds on macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-30
completed:
---
# BL-4370 — Fix CI failure ConnectAsync_ToARealRefusedLoopbackPortOffWindows_FailsWithExit7InUnder500Milliseconds on macOS

## Goal

`ConnectAsync_ToARealRefusedLoopbackPortOffWindows_FailsWithExit7InUnder500Milliseconds` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `ConnectAsync_ToARealRefusedLoopbackPortOffWindows_FailsWithExit7InUnder500Milliseconds` failed on macOS in CI run 41128846186 (https://github.com/StewartScottRogers/Curl/actions/runs/41128846186). First failing commit: 192f7e45.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 41128846186 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `ConnectAsync_ToARealRefusedLoopbackPortOffWindows_FailsWithExit7InUnder500Milliseconds` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: f1e52f1e.
