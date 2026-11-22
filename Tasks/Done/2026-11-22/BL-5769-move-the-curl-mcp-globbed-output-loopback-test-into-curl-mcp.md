---
id: BL-5769
title: Move the Curl.Mcp globbed-output loopback test into Curl.Mcp.IntegrationTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Mcp.UnitTests/InProcessCurlCommandRunnerTests.cs, Curl.Mcp.IntegrationTests/InProcessCurlCommandRunnerIntegrationTests.cs]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-5769 — Move the Curl.Mcp globbed-output loopback test into Curl.Mcp.IntegrationTests

## Goal

The test that runs a globbed `-o` into a missing folder against a loopback HTTP server runs only as an Integration test, in `Curl.Mcp.IntegrationTests`, not in the fast tests.

## Context

- `Curl.Mcp.UnitTests/InProcessCurlCommandRunnerTests.cs:55-86` (`RunAsync_GlobbedOutputIntoAMissingFolderAfterAConnection_GivesCurlsExitCodeWithoutThrowing`, added this shift by BL-5761) starts a `TcpListener`, reads curl's request and writes an HTTP response. Bytes cross the socket, so by CLAUDE.md and ADR-0421 it is an Integration test. It sits in a `*.UnitTests` project with no `[TestCategory("Integration")]`, so the fast run does real socket I/O and coverage counts the lines it reaches.
- Before this shift nothing in `Curl.Mcp.UnitTests` opened a listener. `Curl.Mcp.IntegrationTests` already exists and has a `LoopbackHttpResponder.cs` helper.
- The data-row test above it (`RunAsync_FailingArguments_GiveAnExitCodeWithoutThrowing`) only connects to a refused port 1 and sends nothing, so it stays.
- Found by the shift-end review of 2026-11-22 (pr-review-toolkit code-reviewer, review-20261122-144529).

## Acceptance criteria

- [x] `Curl.Mcp.UnitTests/InProcessCurlCommandRunnerTests.cs` no longer contains a test that opens a `TcpListener`.
- [x] An equivalent test in `Curl.Mcp.IntegrationTests/InProcessCurlCommandRunnerIntegrationTests.cs` carries `[TestCategory("Integration")]`, uses `LoopbackHttpResponder`, and pins exit 23 and non-empty stderr.
- [x] Curl.Mcp stays at 100% line and branch coverage from the fast tests alone; if a line was reached only by the moved test, a unit test with no socket I/O covers it.
- [x] `dotnet build` is clean, and the fast tests and `dotnet test Curl.Mcp.IntegrationTests --filter "TestCategory=Integration"` pass.

## Notes

- The test had already moved to Curl.Mcp.IntegrationTests in be3bf535db; this run renamed the file to InProcessCurlCommandRunnerIntegrationTests.cs and made the test use LoopbackHttpResponder (served in a loop, since curl opens more than one connection). No production code changed; Curl.Mcp.UnitTests has no TcpListener and its 665 fast tests pass.

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: 13d8430b 29ffcf4c e2726449 f2bc4630.
- 2026-11-22: Doing -> Done. Test lives in Curl.Mcp.IntegrationTests using LoopbackHttpResponder; fast and integration tests pass
