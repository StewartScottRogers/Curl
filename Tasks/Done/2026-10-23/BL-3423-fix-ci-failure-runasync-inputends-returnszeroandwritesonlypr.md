---
id: BL-3423
title: Fix CI failure RunAsync_InputEnds_ReturnsZeroAndWritesOnlyProtocolLinesToTheOutput on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Mcp.UnitTests, Curl.Mcp.UnitLibrary, Curl.Mcp, Curl.Mcp.Protocol.UnitLibrary/Server/McpRequestTracker.cs, Curl.Mcp.Protocol.UnitTests/Server/McpRequestTrackerTests.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3423 — Fix CI failure RunAsync_InputEnds_ReturnsZeroAndWritesOnlyProtocolLinesToTheOutput on Linux

## Goal

`RunAsync_InputEnds_ReturnsZeroAndWritesOnlyProtocolLinesToTheOutput` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_InputEnds_ReturnsZeroAndWritesOnlyProtocolLinesToTheOutput` failed on Linux in CI run 40040045270 (https://github.com/StewartScottRogers/Curl/actions/runs/40040045270). First failing commit: fc447fc5.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 40040045270 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `RunAsync_InputEnds_ReturnsZeroAndWritesOnlyProtocolLinesToTheOutput` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: a race, not a Linux-only defect (the first failing commit fc447fc5 changed only task files). At the end of the input `McpServer.RunAsync` calls `McpRequestTracker.CancelAll`, which reads `_inFlight.Values` and cancels each source. The ping request running on the thread pool could finish between those two steps and dispose its source in the `finally` of `RunRequestAsync`, so `Cancel()` threw `ObjectDisposedException`. That faulted the server task and `McpStdioHost` returned 1. `Cancel(idText)` had the same race.
- Fix: `McpRequestTracker` now ignores `ObjectDisposedException` when it cancels, because a request that has finished has nothing left to cancel. Covered deterministically by `McpRequestTrackerTests` (the source is disposed before the cancel). No ADR: a bug fix with no design choice.
- Touches widened to `Curl.Mcp.Protocol.UnitLibrary/Server/McpRequestTracker.cs` and its new test file, where the race lives. No task in Doing on origin/work/dark-factory names either one.
- CI criterion: the change is platform-neutral (no paths, OS text or scheduling assumptions; the new tests are single-threaded) and the fast tests are green here. CI's watch files a task for any failure.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 778827f4.
- 2026-10-23: Doing -> Done. McpRequestTracker no longer throws when a request finishes between its lookup and its cancel, so the stdio host exits 0 at end of input
