---
id: BL-4535
title: Fix McpClient conformance: disconnect on an unsupported protocol version
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.McpClient.UnitLibrary/Client/McpClientFlow.cs, Curl.Extended.Protocol.McpClient.UnitTests/Client, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4535 — Fix McpClient conformance: disconnect on an unsupported protocol version

## Goal

When the server's initialize result names a protocol version Curl does not support (anything but 2025-06-18), curl sends no notifications/initialized and no further request and exits 8 (ADR-1190).

## Context

Found by BL-4482's conformance case McpLifecycleCases.ServerAnswersAVersionCurlDoesNotSupport_CurlDisconnectsWithExit8 for McpClient:MCP20250618-Lifecycle-6 (Model Context Protocol 2025-06-18; matrix Documentation/Conformance/Extended/McpClient.md, ADR-1190). The case is listed in Curl.Conformance.Extended.UnitTests/KnownFailures.txt and passes while it fails. Library: Curl.Extended.Protocol.McpClient.UnitLibrary.

## Acceptance criteria

- [x] McpLifecycleCases.ServerAnswersAVersionCurlDoesNotSupport_CurlDisconnectsWithExit8 passes with its KnownFailures.txt line (McpClient:MCP20250618-Lifecycle-6 BL-4514) removed.
- [x] A focused unit test in Curl.Extended.Protocol.McpClient.UnitTests pins the fix (ADR-0482).
- [x] dotnet build is clean, the fast tests are green and the library stays at 100% line and branch coverage.

## Notes

- InitializeAsync checks the initialize result's protocolVersion against McpRequestWriter.LatestProtocolVersion (2025-06-18) before UseProtocolVersion and throws McpClientException(WeirdServerReply, "MCP server answered unsupported protocol version <v>"), so no notifications/initialized or tools request is sent (ADR-1190). Error text is Curl's own (an extension, no curl to measure). Pinned by McpProtocolHandlerTests.ExecuteAsync_ServerAnswersAnUnsupportedProtocolVersion_SendsNothingMoreAndEndsWithExit8; KnownFailures line removed and the conformance case passes. Measure-CodeQuality for the library: 0 failing members. Platform-neutral (no paths or OS text).

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: d7ef5625 80f495c6 a9b118e0.
- 2026-11-02: Doing -> Done. Client refuses an unsupported protocol version with exit 8
