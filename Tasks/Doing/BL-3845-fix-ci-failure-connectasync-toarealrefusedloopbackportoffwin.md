---
id: BL-3845
title: Fix CI failure ConnectAsync_ToARealRefusedLoopbackPortOffWindows_FailsWithExit7InUnder500Milliseconds on macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-26
completed:
---
# BL-3845 — Fix CI failure ConnectAsync_ToARealRefusedLoopbackPortOffWindows_FailsWithExit7InUnder500Milliseconds on macOS

## Goal

`ConnectAsync_ToARealRefusedLoopbackPortOffWindows_FailsWithExit7InUnder500Milliseconds` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `ConnectAsync_ToARealRefusedLoopbackPortOffWindows_FailsWithExit7InUnder500Milliseconds` failed on macOS in CI run 40357305225 (https://github.com/StewartScottRogers/Curl/actions/runs/40357305225). First failing commit: a6297afb.

    Assertion failed. Expected value to be less than the upper bound.

Lanes test only on Windows, so reproduce with `gh run view 40357305225 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `ConnectAsync_ToARealRefusedLoopbackPortOffWindows_FailsWithExit7InUnder500Milliseconds` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: f1e52f1e.
