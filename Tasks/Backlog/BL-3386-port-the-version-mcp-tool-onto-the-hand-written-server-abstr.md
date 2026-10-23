---
id: BL-3386
title: Port the version MCP tool onto the hand-written server abstractions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3375, BL-3378, BL-3374]
touches: [Curl.Mcp/Tools/VersionMcpTool.cs, Curl.Mcp/Tools/VersionMcpToolJson.cs, Curl.Mcp.UnitTests/Tools/VersionMcpToolTests.cs]
model: sonnet
requirement: none
created: 2026-10-23
completed:
---
# BL-3386 — Port the version MCP tool onto the hand-written server abstractions

## Goal

`VersionMcpTool` implements `IMcpTool` for `version` with the same name, description, `readOnlyHint`/`idempotentHint` annotations, empty input schema and result as the SDK-attributed `CurlVersionTool`.

## Context

- Read `Curl.Mcp/CurlVersionTool.cs` and `Curl.Mcp.UnitTests/CurlVersionToolTests.cs`; do not edit or delete them (the cut-over task does). Logic stays in `McpVersionTool`.
- The tool takes no arguments; its schema is written by hand and must equal the golden `version` entry of BL-3374 as a JSON value (the SDK emits an object schema with no properties). Arguments supplied anyway are ignored as the golden files show. A tiny source-generated context in `VersionMcpToolJson.cs` holds `McpVersionToolResult`.

## Acceptance criteria

- [ ] `VersionMcpTool` exists with name, description and annotations equal to `CurlVersionTool`'s and an input schema equal to the golden one as a JSON value.
- [ ] Every test case of `CurlVersionToolTests.cs` has an equivalent in `VersionMcpToolTests.cs`, and a test proves the serialized result equals `CurlVersionTool`'s.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Mcp,Curl.Mcp.UnitLibrary` reports 0 failing members (100% line, 100% branch, complexity at most 10, CRAP at most 30), measured from the fast tests only.
- [ ] `dotnet build` of every project named in touches is clean with `-warnaserror`, and `dotnet test --filter "TestCategory!=Integration"` passes for the test projects named in touches, with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-23: Created.
