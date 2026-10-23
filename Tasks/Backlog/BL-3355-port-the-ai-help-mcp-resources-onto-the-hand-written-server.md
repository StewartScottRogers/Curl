---
id: BL-3355
title: Port the --ai-help MCP resources onto the hand-written server abstractions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3343, BL-3346, BL-3342]
touches: [Curl.Mcp/Tools/AiHelpMcpResourceProvider.cs, Curl.Mcp.UnitTests/Tools/AiHelpMcpResourceProviderTests.cs]
model: sonnet
requirement: none
created: 2026-10-23
completed:
---
# BL-3355 — Port the --ai-help MCP resources onto the hand-written server abstractions

## Goal

`AiHelpMcpResourceProvider` implements `IMcpResourceProvider` over `McpAiHelpResourceCatalogue` with the same list entries, read contents and not-found behaviour as `CurlAiHelpResourceHandlers`.

## Context

- Read `Curl.Mcp/CurlAiHelpResourceHandlers.cs`, `Curl.Mcp.UnitTests/CurlAiHelpResourceHandlersTests.cs` and ADR-0589; do not edit or delete the old file or tests (the cut-over task does).
- List: every catalogue resource with `uri`, `name`, `description` and `mimeType` `McpAiHelpResource.MimeType`. Read: the resource's Markdown as one text content with `uri` and the same MIME type; an unknown or empty URI is the not-found failure with the message `Resource not found: '<uri>'.` (the handler from the resources task turns it into code -32002). Golden files of BL-3342 decide anything ambiguous.

## Acceptance criteria

- [ ] `AiHelpMcpResourceProvider` exists, with list and read results equal to `CurlAiHelpResourceHandlers`' for every catalogue resource, asserted by a test while both exist.
- [ ] Every test case of `CurlAiHelpResourceHandlersTests.cs` has an equivalent in `AiHelpMcpResourceProviderTests.cs`, including an unknown URI and an empty URI.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Mcp,Curl.Mcp.UnitLibrary` reports 0 failing members (100% line, 100% branch, complexity at most 10, CRAP at most 30), measured from the fast tests only.
- [ ] `dotnet build` of every project named in touches is clean with `-warnaserror`, and `dotnet test --filter "TestCategory!=Integration"` passes for the test projects named in touches, with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-23: Created.
