---
id: BL-3332
title: Fix CI failure RunAsync_StalledBelowSpeedLimitForSpeedTime_Exits28WithCurlsMessage on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests, Curl.Console]
requirement: none
created: 2026-10-23
completed:
---
# BL-3332 — Fix CI failure RunAsync_StalledBelowSpeedLimitForSpeedTime_Exits28WithCurlsMessage on Linux

## Goal

`RunAsync_StalledBelowSpeedLimitForSpeedTime_Exits28WithCurlsMessage` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_StalledBelowSpeedLimitForSpeedTime_Exits28WithCurlsMessage` failed on Linux in CI run 39986660283 (https://github.com/StewartScottRogers/Curl/actions/runs/39986660283). First failing commit: 13c20167.

    Assertion failed. Expected condition to be false.

Lanes test only on Windows, so reproduce with `gh run view 39986660283 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `RunAsync_StalledBelowSpeedLimitForSpeedTime_Exits28WithCurlsMessage` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 2ae4e460.
