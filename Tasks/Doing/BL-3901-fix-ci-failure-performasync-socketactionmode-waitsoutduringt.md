---
id: BL-3901
title: Fix CI failure PerformAsync_SocketActionMode_WaitsOutDuringTheDialAndMovesBytesOnlyWhenSignalled on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests, Curl.Engine.UnitLibrary]
requirement: none
created: 2026-10-26
completed:
---
# BL-3901 — Fix CI failure PerformAsync_SocketActionMode_WaitsOutDuringTheDialAndMovesBytesOnlyWhenSignalled on Linux

## Goal

`PerformAsync_SocketActionMode_WaitsOutDuringTheDialAndMovesBytesOnlyWhenSignalled` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `PerformAsync_SocketActionMode_WaitsOutDuringTheDialAndMovesBytesOnlyWhenSignalled` failed on Linux in CI run 40423012883 (https://github.com/StewartScottRogers/Curl/actions/runs/40423012883). First failing commit: 22de86cf.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 40423012883 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `PerformAsync_SocketActionMode_WaitsOutDuringTheDialAndMovesBytesOnlyWhenSignalled` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 2338d4f0.
