---
id: BL-2490
title: Fix flaky Curl.Mcp test ToolOptions_IsBuiltOnceAndThenShared under parallel runs
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Mcp, Curl.Mcp.UnitTests]
requirement: none
created: 2026-10-16
completed:
---
# BL-2490 — Fix flaky Curl.Mcp test ToolOptions_IsBuiltOnceAndThenShared under parallel runs

## Goal

`Curl.Mcp.UnitTests`' `CurlMcpJsonContextTests.ToolOptions_IsBuiltOnceAndThenShared` passes every run, alone and in the whole project's method-parallel run.

## Context

During BL-2422's `Measure-CodeQuality.ps1` run on 2026-10-16 (lane 2) it failed in the full `Curl.Mcp.UnitTests` run with `Expected both values to refer to the same object` (1 of 476) and passed when run alone. ADR-0621 builds `CurlMcpJsonContext.ToolOptions` on first use; two tests touching it at once can each build one, so the first-use build is not thread-safe. Make it so (`Lazy<T>` or `LazyInitializer.EnsureInitialized` with a lock), or show the cause is elsewhere.

## Acceptance criteria

- [ ] `CurlMcpJsonContext.ToolOptions` is built exactly once even when first read from several threads at once, pinned by a test that reads it from parallel tasks.
- [ ] `dotnet build` is clean and the fast tests pass.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Mcp` reports 0 failing members.

## Notes

## Log

- 2026-10-16: Created.
