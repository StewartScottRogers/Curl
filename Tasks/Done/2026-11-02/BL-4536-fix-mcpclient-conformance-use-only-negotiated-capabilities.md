---
id: BL-4536
title: Fix McpClient conformance: use only negotiated capabilities
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.McpClient.UnitLibrary/Client/McpClientFlow.cs, Curl.Extended.Protocol.McpClient.UnitLibrary/Wire/McpInitializeResult.cs, Curl.Extended.Protocol.McpClient.UnitLibrary/Wire/McpResponseReader.cs, Curl.Extended.Protocol.McpClient.UnitTests/Wire/McpResponseReaderTests.cs, Curl.Extended.Protocol.McpClient.UnitTests/Client, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4536 — Fix McpClient conformance: use only negotiated capabilities

## Goal

When the server's initialize result declares no tools capability, curl sends no tools/list or tools/call and exits 8 (ADR-1190).

## Context

Found by BL-4482's conformance case McpLifecycleCases.ServerDeclaresNoToolsCapability_CurlSendsNoToolsRequestAndExits8 for McpClient:MCP20250618-Lifecycle-7 (Model Context Protocol 2025-06-18; matrix Documentation/Conformance/Extended/McpClient.md, ADR-1190). The case is listed in Curl.Conformance.Extended.UnitTests/KnownFailures.txt and passes while it fails. Library: Curl.Extended.Protocol.McpClient.UnitLibrary.

## Acceptance criteria

- [x] McpLifecycleCases.ServerDeclaresNoToolsCapability_CurlSendsNoToolsRequestAndExits8 passes with its KnownFailures.txt line (McpClient:MCP20250618-Lifecycle-7 BL-4515) removed.
- [x] A focused unit test in Curl.Extended.Protocol.McpClient.UnitTests pins the fix (ADR-0482).
- [x] dotnet build is clean, the fast tests are green and the library stays at 100% line and branch coverage.

## Notes

- Touches widened to the Wire reader (McpInitializeResult.cs, McpResponseReader.cs) and its tests: the InitializeResult reader only kept protocolVersion, and reading capabilities belongs there, not in the flow. No task in Doing on origin/work/dark-factory held them.
- Decision (within ADR-1190, exit 8): `McpResponseReader.ReadInitializeResult` reports `DeclaresTools` (true only when `capabilities.tools` is an object; a missing or malformed `capabilities` declares nothing rather than failing, so a lax server gets the specific message). `McpClientFlow.InitializeAsync` ends with exit 8 "MCP server declares no tools capability" before `notifications/initialized`, like the unsupported-version case, since every request curl makes is a tools request.
- Pinned by McpProtocolHandlerTests.ExecuteAsync_ServerDeclaresNoToolsCapability_SendsNoToolsRequestAndEndsWithExit8 and McpResponseReaderTests.ReadInitializeResult_CapabilityNegotiationWithoutToolsObject_DeclaresNoTools. Measure-CodeQuality for the library: 100% line, 100% branch, 0 failing members. Platform-neutral (stdio fake, no paths), so CI on Linux and macOS is covered by the green fast tests here.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: a3134be4 80f495c6 a9b118e0.
- 2026-11-02: Doing -> Done. Curl sends no tools request and exits 8 when the server declares no tools capability; Lifecycle-7 passes off KnownFailures
