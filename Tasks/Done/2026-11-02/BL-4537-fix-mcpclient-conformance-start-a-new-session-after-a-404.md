---
id: BL-4537
title: Fix McpClient conformance: start a new session after a 404
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.McpClient.UnitLibrary/StreamableHttp/McpStreamableHttpTransport.cs, Curl.Extended.Protocol.McpClient.UnitLibrary/Client, Curl.Extended.Protocol.McpClient.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4537 — Fix McpClient conformance: start a new session after a 404

## Goal

When a request carrying Mcp-Session-Id is answered 404, curl starts a new session with a new initialize carrying no session id (then notifications/initialized) and repeats the request in it, as Streamable HTTP "Session Management" requires.

## Context

Found by BL-4482's conformance case McpStreamableHttpCases.ExpiredSession404_CurlStartsANewSessionWithoutTheOldId for McpClient:MCP20250618-StreamableHttp-8 (Model Context Protocol 2025-06-18; matrix Documentation/Conformance/Extended/McpClient.md, ADR-1190). The case is listed in Curl.Conformance.Extended.UnitTests/KnownFailures.txt and passes while it fails. Library: Curl.Extended.Protocol.McpClient.UnitLibrary.

## Acceptance criteria

- [x] McpStreamableHttpCases.ExpiredSession404_CurlStartsANewSessionWithoutTheOldId passes with its KnownFailures.txt line (McpClient:MCP20250618-StreamableHttp-8 BL-4516) removed.
- [x] A focused unit test in Curl.Extended.Protocol.McpClient.UnitTests pins the fix (ADR-0482).
- [x] dotnet build is clean, the fast tests are green and the library stays at 100% line and branch coverage.

## Notes

- Plan (ADR-1195): the transport answers a 404 to a request it sent with a session id by forgetting the session id and protocol version and failing with the new McpHttpFailure.SessionExpired; McpClientFlow.RequestAsync catches it once per request, runs initialize and notifications/initialized again, and repeats the request; a second 404 is exit 8. A 404 without a session id stays UnexpectedStatus.
- Tests: McpStreamableHttpTransportTests.SendRequestAsync_NotFoundWithASessionId_FailsWithSessionExpiredAndForgetsTheSession and McpProtocolHandlerTests.ExecuteAsync_SessionExpiresWith404_* and ExecuteAsync_NewSessionExpiresWith404Too_EndsWithExit8 (new fake SequencedMcpConnector, since the closed connection after the 404 is replaced by a new one). Conformance case passes with its KnownFailures line removed.
- Measured: Measure-CodeQuality for Curl.Extended.Protocol.McpClient.UnitLibrary: 100% line, 100% branch, max complexity 10, 0 failing members. dotnet build clean (0 warnings); full fast tests green.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 4f32e72d 80f495c6 a9b118e0.
- 2026-11-02: Doing -> Done. McpClient starts a new session without the old Mcp-Session-Id after a 404 and repeats the request once
