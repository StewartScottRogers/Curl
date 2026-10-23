---
id: BL-3346
title: Define the tool, tool-result and resource abstractions of the hand-written MCP server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3344]
touches: [Curl.Mcp.Protocol.UnitLibrary/Model, Curl.Mcp.Protocol.UnitTests/Model]
model: opus
requirement: none
created: 2026-10-23
completed:
---
# BL-3346 — Define the tool, tool-result and resource abstractions of the hand-written MCP server

## Goal

`Curl.Mcp.Protocol.UnitLibrary/Model/` defines what a tool and a resource are to the server: `IMcpTool` (name, description, hand-written input schema, annotations, an async call taking the arguments object and a cancellation token), the tool-call result (text content blocks and `isError`), `IMcpResourceProvider` (list, read, not found), and a registry the host fills, with source-generated JSON for their wire shapes.

## Context

- This is the seam the port tasks implement, one file per tool; keep it small and stable.
- The wire shapes must serialize exactly as the golden files of BL-3342 show: `tools/list` entries (`name`, `description`, `inputSchema`, `annotations` with `readOnlyHint`/`idempotentHint` as the SDK emitted them for `version`, `parse_url` and `build_url`); `CallToolResult` (`content` text blocks, `isError`); `resources/list` entries (`uri`, `name`, `description`, `mimeType`); `resources/read` `contents` (`uri`, `mimeType`, `text`). Optional properties are omitted when null or default exactly as the SDK omitted them. If BL-3342 is not Done yet, follow the MCP specification and leave the golden comparison to the final task.
- The input schema is supplied per tool as raw JSON text (a constant), parsed once and validated to be a JSON object with `type: object` at registration; never reflected or generated from a method signature.
- The registry is a plain class the host fills one `Add` per tool or provider; duplicates are refused. No reflection scanning anywhere.
- Source-generated JSON, one context in its own file; tests in `Curl.Mcp.Protocol.UnitTests/Model/` assert literal JSON for each shape.

## Acceptance criteria

- [ ] `IMcpTool`, the tool-call result and content types, `IMcpResourceProvider`, the resource and resource-contents types and the registry (duplicate tool names and resource URIs refused) exist under `Model/`, one type per file, with XML doc comments.
- [ ] Serialization tests assert the exact JSON of each wire shape, including omitted-when-null properties.
- [ ] Registering a tool whose schema is not a JSON object with `type: object` throws a documented exception, covered by a test.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Mcp.Protocol.UnitLibrary` reports 0 failing members (100% line, 100% branch, complexity at most 10, CRAP at most 30), measured from the fast tests only.
- [ ] `dotnet build` of every project named in touches is clean with `-warnaserror`, and `dotnet test --filter "TestCategory!=Integration"` passes for the test projects named in touches, with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-23: Created.
