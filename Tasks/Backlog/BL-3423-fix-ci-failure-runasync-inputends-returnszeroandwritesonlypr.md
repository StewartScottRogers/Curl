---
id: BL-3423
title: Fix CI failure RunAsync_InputEnds_ReturnsZeroAndWritesOnlyProtocolLinesToTheOutput on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Mcp.UnitTests, Curl.Mcp.UnitLibrary, Curl.Mcp]
requirement: none
created: 2026-10-23
completed:
---
# BL-3423 — Fix CI failure RunAsync_InputEnds_ReturnsZeroAndWritesOnlyProtocolLinesToTheOutput on Linux

## Goal

`RunAsync_InputEnds_ReturnsZeroAndWritesOnlyProtocolLinesToTheOutput` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_InputEnds_ReturnsZeroAndWritesOnlyProtocolLinesToTheOutput` failed on Linux in CI run 40040045270 (https://github.com/StewartScottRogers/Curl/actions/runs/40040045270). First failing commit: fc447fc5.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 40040045270 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `RunAsync_InputEnds_ReturnsZeroAndWritesOnlyProtocolLinesToTheOutput` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-23: Created.
