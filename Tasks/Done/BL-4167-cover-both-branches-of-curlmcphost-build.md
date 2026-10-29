---
id: BL-4167
title: Cover both branches of CurlMcpHost.Build()
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Mcp/CurlMcpHost.cs, Curl.Mcp.UnitTests/CurlMcpHostTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4167 — Cover both branches of CurlMcpHost.Build()

## Goal

`CurlMcpHost.Build()` (Curl.Mcp/CurlMcpHost.cs:31) meets the branch gate.

## Context

BL-4098's Measure-CodeQuality.ps1 run (Curl.Engine.UnitLibrary, Curl.Api, Curl.Mcp) on 2026-10-29 reported `CurlMcpHost.Build()` at 100% line, 50% branch, Cx 2: a failing member BL-4098 did not change. The likely cause is the compiler-cached delegate of the `Environment.GetEnvironmentVariable` method group (a null-check branch only a second call takes); confirm before fixing.

## Acceptance criteria

- [x] `Measure-CodeQuality.ps1 -Library Curl.Mcp` reports no failing member.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Confirmed the cause: only one fast test called `Build()`, so the branch of the compiler-cached delegate for the `Environment.GetEnvironmentVariable` method group, taken once the cache is set, never ran. The fix is in the tests only: `Build_OverTheProcessTwice_BuildsTwoSeparateHosts` calls `Build()` twice. `CurlMcpHost.cs` is unchanged.
- `Measure-CodeQuality.ps1 -Library Curl.Mcp` reported 0 failing members. Build is clean and the fast tests are green on Windows. The new test does not depend on the platform.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: f4190bab 3d836d5e.
- 2026-10-29: Doing -> Done. CurlMcpHost.Build() meets the branch gate: a test now takes the cached-delegate branch
