---
id: BL-3351
title: Port the curl MCP tool onto the hand-written server abstractions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3343, BL-3346, BL-3342]
touches: [Curl.Mcp/Tools/CurlCommandMcpTool.cs, Curl.Mcp/Tools/CurlCommandMcpToolJson.cs, Curl.Mcp.UnitTests/Tools/CurlCommandMcpToolTests.cs]
model: sonnet
requirement: none
created: 2026-10-23
completed:
---
# BL-3351 — Port the curl MCP tool onto the hand-written server abstractions

## Goal

`Curl.Mcp/Tools/CurlCommandMcpTool.cs` implements `IMcpTool` for the `curl` tool with the same name, description, annotations, input schema and results as the SDK-attributed `CurlCommandTool`, beside it, so the cut-over task can swap one for the other.

## Context

- Read the existing `Curl.Mcp/CurlCommandTool.cs` and its tests `Curl.Mcp.UnitTests/CurlCommandToolTests.cs`; do not edit or delete them (the cut-over task does). Leave `McpCurlTool` in `Curl.Mcp.UnitLibrary` untouched: this is only the thin adapter.
- Copy the description text verbatim. Write the input schema by hand as a constant: `args` (array of string, required, with its description), `stdin` (string, optional, with its description), and whatever else the golden `tools/list` of BL-3342 shows for `curl` (additional-properties flag, `title`, `required`); the schema must equal the golden one as a JSON value.
- Arguments are read from the `JsonElement` object: a missing or wrongly-typed `args` is the same answer the golden files show for a missing required argument (record any difference in Notes). Result JSON comes from a small source-generated context in `CurlCommandMcpToolJson.cs` (its own file, so tool ports stay independent); the text block is byte-identical to what `CurlCommandTool` produced for the same `McpCurlToolResult`.
- Tests: for every case in `CurlCommandToolTests.cs` an equivalent test of the new class, plus a test that the new tool's serialized result equals the old class's for the same inputs while both exist.

## Acceptance criteria

- [ ] `CurlCommandMcpTool` exists, `Name == "curl"`, the description and annotations equal `CurlCommandTool`'s, and its input schema equals the golden `tools/list` schema for `curl` as a JSON value (test reads the golden file or a copy).
- [ ] Every test case of `CurlCommandToolTests.cs` has an equivalent in `CurlCommandMcpToolTests.cs`; the existing old tests still pass untouched.
- [ ] Wrong-typed or missing `args` is answered as the golden file shows, or the difference is listed in Notes.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Mcp,Curl.Mcp.UnitLibrary` reports 0 failing members (100% line, 100% branch, complexity at most 10, CRAP at most 30), measured from the fast tests only.
- [ ] `dotnet build` of every project named in touches is clean with `-warnaserror`, and `dotnet test --filter "TestCategory!=Integration"` passes for the test projects named in touches, with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-23: Created.
