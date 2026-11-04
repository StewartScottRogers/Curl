---
id: BL-4804
title: Fix CI failure RunAsync_RequestOutlivesTheShutdownTimeout_LogsItAndReturnsOne on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Mcp.UnitTests, Curl.Mcp.UnitLibrary, Curl.Mcp]
requirement: none
created: 2026-11-03
completed:
---
# BL-4804 — Fix CI failure RunAsync_RequestOutlivesTheShutdownTimeout_LogsItAndReturnsOne on Windows

## Goal

`RunAsync_RequestOutlivesTheShutdownTimeout_LogsItAndReturnsOne` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_RequestOutlivesTheShutdownTimeout_LogsItAndReturnsOne` failed on Windows in CI run 41764281440 (https://github.com/StewartScottRogers/Curl/actions/runs/41764281440). First failing commit: 48505d3b.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 41764281440 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `RunAsync_RequestOutlivesTheShutdownTimeout_LogsItAndReturnsOne` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 4d5996de.
