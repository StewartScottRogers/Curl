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
completed:
---
# BL-4537 — Fix McpClient conformance: start a new session after a 404

## Goal

When a request carrying Mcp-Session-Id is answered 404, curl starts a new session with a new initialize carrying no session id (then notifications/initialized) and repeats the request in it, as Streamable HTTP "Session Management" requires.

## Context

Found by BL-4482's conformance case McpStreamableHttpCases.ExpiredSession404_CurlStartsANewSessionWithoutTheOldId for McpClient:MCP20250618-StreamableHttp-8 (Model Context Protocol 2025-06-18; matrix Documentation/Conformance/Extended/McpClient.md, ADR-1190). The case is listed in Curl.Conformance.Extended.UnitTests/KnownFailures.txt and passes while it fails. Library: Curl.Extended.Protocol.McpClient.UnitLibrary.

## Acceptance criteria

- [ ] McpStreamableHttpCases.ExpiredSession404_CurlStartsANewSessionWithoutTheOldId passes with its KnownFailures.txt line (McpClient:MCP20250618-StreamableHttp-8 BL-4516) removed.
- [ ] A focused unit test in Curl.Extended.Protocol.McpClient.UnitTests pins the fix (ADR-0482).
- [ ] dotnet build is clean, the fast tests are green and the library stays at 100% line and branch coverage.

## Notes

## Log

- 2026-11-02: Created.
