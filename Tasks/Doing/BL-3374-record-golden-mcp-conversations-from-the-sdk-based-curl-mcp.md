---
id: BL-3374
title: Record golden MCP conversations from the SDK-based Curl.Mcp server and replay them as an Integration test
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Mcp.IntegrationTests/Golden, Curl.Mcp.IntegrationTests/CurlMcpGoldenConversationIntegrationTests.cs]
model: sonnet
requirement: none
created: 2026-10-23
completed:
---
# BL-3374 — Record golden MCP conversations from the SDK-based Curl.Mcp server and replay them as an Integration test

## Goal

A recorded set of MCP conversations with the current SDK-based `Curl.Mcp` server is committed as golden files, and an Integration test replays them against the built server and passes, so the hand-written server can later be proved to answer the same.

## Context

- Part of the plan to drop `ModelContextProtocol` and `Microsoft.Extensions.Hosting` from `Curl.Mcp` (Stewart, 2026-10-23; decision BL-3373). This task must be Done before any task replaces the SDK host: the cut-over task depends on it. It changes no product code.
- Use the existing hand-written client `Curl.Mcp.IntegrationTests/McpStdioClient.cs` and the loopback helper `LoopbackHttpResponder.cs`; read `CurlMcpServerIntegrationTests.cs` and `CurlTransferToolIntegrationTests.cs` for how they start the server with `CURL_MCP_ROOT`.
- Record, as one JSON file per conversation under `Curl.Mcp.IntegrationTests/Golden/`: `initialize` with each client protocol version the SDK accepts (at least the latest the SDK supports, one older, and one it does not know, to show negotiation) followed by `notifications/initialized`; `ping`; `tools/list` (all five tools: `curl`, `transfer`, `parse_url`, `build_url`, `version`, with description, inputSchema and annotations); `tools/call` for each tool with a success and a failure case (the `curl` tool with a nonzero exit, `transfer` to a closed loopback port, `parse_url` on a refused URL, a path outside `CURL_MCP_ROOT`, a `transfer` argument of the wrong JSON shape, an unknown tool name, a missing required argument); `resources/list`; `resources/read` for one resource and for a missing URI; an unknown method; a request before `initialize`; malformed JSON; a non-object message and a batch array if the SDK answers them.
- Normalise only what legitimately varies (timings, ports, temp paths, `elapsed` fields) by named placeholders; record the rules in the test class doc comment. Responses are compared as JSON values with object property order ignored and arrays in order.
- Where the SDK's text is clearly accidental (a stack trace, a generic "An error occurred invoking"), still record it; the porting tasks decide whether to match it or list a deliberate difference.

## Acceptance criteria

- [ ] `Curl.Mcp.IntegrationTests/Golden/` holds one file per conversation listed in Context, each with the request lines sent and the response lines received.
- [ ] `Curl.Mcp.IntegrationTests/CurlMcpGoldenConversationIntegrationTests.cs` replays every golden file against the built `Curl.Mcp` and passes with `dotnet test Curl.Mcp.IntegrationTests --filter "TestCategory=Integration&FullyQualifiedName~Golden"`; the class carries `[TestCategory("Integration")]`.
- [ ] The golden `tools/list` file lists exactly the five tools with their current descriptions, input schemas and annotations.
- [ ] The tests are platform-neutral (no drive-letter path, no Windows-only text), per root CLAUDE.md.
- [ ] `dotnet build` of every project named in touches is clean with `-warnaserror`, and `dotnet test --filter "TestCategory!=Integration"` passes for the test projects named in touches, with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 4d3a4efe 42864ae2 61e7c922 88ed799f 6d2cec38.
