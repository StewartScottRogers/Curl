---
id: BL-3385
title: Port the parse_url and build_url MCP tools onto the hand-written server abstractions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3375, BL-3378, BL-3374]
touches: [Curl.Mcp/Tools/ParseUrlMcpTool.cs, Curl.Mcp/Tools/BuildUrlMcpTool.cs, Curl.Mcp/Tools/UrlMcpToolJson.cs, Curl.Mcp.UnitTests/Tools/ParseUrlMcpToolTests.cs, Curl.Mcp.UnitTests/Tools/BuildUrlMcpToolTests.cs]
model: sonnet
requirement: none
created: 2026-10-23
completed:
---
# BL-3385 — Port the parse_url and build_url MCP tools onto the hand-written server abstractions

## Goal

`ParseUrlMcpTool` and `BuildUrlMcpTool` implement `IMcpTool` for `parse_url` and `build_url` with the same names, descriptions, `readOnlyHint`/`idempotentHint` annotations, input schemas and results as the SDK-attributed `CurlUrlTool`.

## Context

- Read `Curl.Mcp/CurlUrlTool.cs` and `Curl.Mcp.UnitTests/CurlUrlToolTests.cs`; do not edit or delete them (the cut-over task does). Logic stays in `McpUrlTool` (`Curl.Mcp.UnitLibrary`).
- Copy the descriptions verbatim (`ParseDescription`, `BuildDescription`, and the shared flags description). Write both input schemas by hand to equal the golden `tools/list` entries of BL-3374 as JSON values (`parse_url`: `url` required, `flags` array of string; `build_url`: `scheme`, `user`, `password`, `options`, `host`, `port`, `path`, `query`, `fragment`, `zoneId`, `flags`, all optional).
- Fields are read from the `JsonElement` arguments; a wrong type or missing `url` answers as the golden files show, or the difference is listed in Notes. A small source-generated context in `UrlMcpToolJson.cs` holds the two result types.

## Acceptance criteria

- [ ] `ParseUrlMcpTool` and `BuildUrlMcpTool` exist, with names, descriptions and annotations equal to `CurlUrlTool`'s and input schemas equal to the golden schemas as JSON values.
- [ ] Every test case of `CurlUrlToolTests.cs` has an equivalent in the two new test files, including a refused URL and a refused part being tool errors with `isError: true`.
- [ ] A test proves the serialized results equal `CurlUrlTool`'s for the same `McpParseUrlToolResult` and `McpBuildUrlToolResult`.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Mcp,Curl.Mcp.UnitLibrary` reports 0 failing members (100% line, 100% branch, complexity at most 10, CRAP at most 30), measured from the fast tests only.
- [ ] `dotnet build` of every project named in touches is clean with `-warnaserror`, and `dotnet test --filter "TestCategory!=Integration"` passes for the test projects named in touches, with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 495211c9 02aabbf9 6cf79b86 96ffe31b 6d2cec38.
