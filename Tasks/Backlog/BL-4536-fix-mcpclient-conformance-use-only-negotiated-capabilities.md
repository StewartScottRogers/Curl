---
id: BL-4536
title: Fix McpClient conformance: use only negotiated capabilities
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.McpClient.UnitLibrary/Client/McpClientFlow.cs, Curl.Extended.Protocol.McpClient.UnitTests/Client, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4536 — Fix McpClient conformance: use only negotiated capabilities

## Goal

When the server's initialize result declares no tools capability, curl sends no tools/list or tools/call and exits 8 (ADR-1190).

## Context

Found by BL-4482's conformance case McpLifecycleCases.ServerDeclaresNoToolsCapability_CurlSendsNoToolsRequestAndExits8 for McpClient:MCP20250618-Lifecycle-7 (Model Context Protocol 2025-06-18; matrix Documentation/Conformance/Extended/McpClient.md, ADR-1190). The case is listed in Curl.Conformance.Extended.UnitTests/KnownFailures.txt and passes while it fails. Library: Curl.Extended.Protocol.McpClient.UnitLibrary.

## Acceptance criteria

- [ ] McpLifecycleCases.ServerDeclaresNoToolsCapability_CurlSendsNoToolsRequestAndExits8 passes with its KnownFailures.txt line (McpClient:MCP20250618-Lifecycle-7 BL-4515) removed.
- [ ] A focused unit test in Curl.Extended.Protocol.McpClient.UnitTests pins the fix (ADR-0482).
- [ ] dotnet build is clean, the fast tests are green and the library stays at 100% line and branch coverage.

## Notes

## Log

- 2026-11-02: Created.
