---
id: BL-5767
title: Move the Curl.Mcp loopback-server globbed-output test into Curl.Mcp.IntegrationTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Mcp.UnitTests/InProcessCurlCommandRunnerTests.cs, Curl.Mcp.IntegrationTests/InProcessCurlCommandRunnerMissingFolderIntegrationTests.cs]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-5767 — Move the Curl.Mcp loopback-server globbed-output test into Curl.Mcp.IntegrationTests

## Goal

`Curl.Mcp.UnitTests` holds no test that exchanges bytes over a real socket. The globbed-output test that runs its own HTTP server on loopback moves to `Curl.Mcp.IntegrationTests` with `[TestCategory("Integration")]`, or becomes a unit test over a scripted connector.

## Context

The shift-end review of 2026-11-22 (ADR-0710) found this in the merge `dfb9516dda...9e2c6390ba`. The code reviewer reported it.

- **The test:** BL-5761 added `RunAsync_GlobbedOutputIntoAMissingFolderAfterAConnection_GivesCurlsExitCodeWithoutThrowing` in `Curl.Mcp.UnitTests/InProcessCurlCommandRunnerTests.cs`, around line 55.
- **What it does:** it starts a `TcpListener` on `127.0.0.1`, reads each request and writes an HTTP 200 response.
- **The rule it breaks:** root `CLAUDE.md` and ADR-0421 allow a unit test only "a loopback socket it opens and closes without sending a byte". A test that touches a real socket is an Integration test and lives only in a `*.IntegrationTests` project.
- **Why that matters:**
  - Lane builds and the fast CI filter run this test today.
  - The `VerifyIntegrationTestPlacement` target cannot see the breach, because the test carries no category.
- **The related data rows:** `RunAsync_FailingArguments_GiveAnExitCodeWithoutThrowing` in the same file, and `Curl.Mcp.UnitTests/McpCurlToolMissingFolderTests.cs`, make connection attempts to `127.0.0.1:1`. Move or rework those rows as well when they dial a real socket.
- **The other option:** the in-process runner can be given a scripted connector if one is available. That keeps the case a unit test and keeps its coverage measured.

## Acceptance criteria

- [x] No test in `Curl.Mcp.UnitTests` creates a `TcpListener` or `TcpClient`. `grep -rn "TcpListener\|TcpClient" Curl.Mcp.UnitTests` finds nothing.
- [x] Either the globbed-output case runs in `Curl.Mcp.IntegrationTests` with `[TestCategory("Integration")]` and still asserts exit 23 with non-empty stderr, or it runs as a unit test over a scripted connector with the same assertions.
- [x] Any data row of the tests in the related data rows above that dials `127.0.0.1:1` is either moved the same way or changed so that it fails before connecting.
- [x] `dotnet build` is clean, including `VerifyIntegrationTestPlacement`. Both the fast and the Integration filters pass, and `Curl.Mcp`'s line and branch coverage from the fast tests does not drop.

## Notes

Globbed-output test and the two `127.0.0.1:1` connection rows (runner pop3 row, tool-level missing-folder test) moved to `Curl.Mcp.IntegrationTests/InProcessCurlCommandRunnerMissingFolderIntegrationTests.cs`; the unknown-option row now uses `http://h/` so it fails before connecting. Chose moving over a scripted connector (none available). No production code changed.

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: e862609f 61a902fe f6777efd 70c924c8.
- 2026-11-22: Doing -> Done. Moved loopback tests to IntegrationTests
