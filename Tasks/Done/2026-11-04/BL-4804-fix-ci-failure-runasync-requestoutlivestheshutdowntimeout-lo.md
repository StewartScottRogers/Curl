---
id: BL-4804
title: Fix CI failure RunAsync_RequestOutlivesTheShutdownTimeout_LogsItAndReturnsOne on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Mcp.UnitTests, Curl.Mcp.UnitLibrary, Curl.Mcp, Curl.Mcp.Protocol.UnitLibrary/Server/McpServer.cs]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4804 — Fix CI failure RunAsync_RequestOutlivesTheShutdownTimeout_LogsItAndReturnsOne on Windows

## Goal

`RunAsync_RequestOutlivesTheShutdownTimeout_LogsItAndReturnsOne` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_RequestOutlivesTheShutdownTimeout_LogsItAndReturnsOne` failed on Windows in CI run 41764281440 (https://github.com/StewartScottRogers/Curl/actions/runs/41764281440). First failing commit: 48505d3b.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 41764281440 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `RunAsync_RequestOutlivesTheShutdownTimeout_LogsItAndReturnsOne` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: a race in `McpServer.RunAsync` (`Curl.Mcp.Protocol.UnitLibrary`). The read loop started a request
  (`Task.Run`) before adding its task to `running` under the lock. The handler could start, the test (or a real
  stop signal) could cancel, and the shutdown's snapshot of `running` could come back empty, so `RunAsync`
  completed without waiting for the request in flight and the host returned 0, not 1. The fix dispatches under
  that same lock, so a started request is always in the snapshot. That also fixes the production behaviour:
  a stop that comes right after a request starts no longer abandons it.
- `touches` widened to `Curl.Mcp.Protocol.UnitLibrary/Server/McpServer.cs`; no task in Doing on
  origin/work/dark-factory names it.
- No new deterministic test: the bad interleaving needs a hook inside the read loop, which a production seam
  added only for a test would not justify. `RunAsync_RequestOutlivesTheShutdownTimeout_LogsItAndReturnsOne`
  is the regression test. It hit the race in CI, and with the fix it cannot.
- CI on Linux and macOS: the change is platform-neutral and the fast tests are green here (lanes cannot see CI).

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 4d5996de.
- 2026-11-03: Doing -> Done. McpServer dispatches each request under the lock the shutdown snapshot takes, so the shutdown-timeout test no longer races
