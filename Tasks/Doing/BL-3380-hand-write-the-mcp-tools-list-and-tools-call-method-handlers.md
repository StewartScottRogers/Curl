---
id: BL-3380
title: Hand-write the MCP tools/list and tools/call method handlers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3378, BL-3379]
touches: [Curl.Mcp.Protocol.UnitLibrary/Tools, Curl.Mcp.Protocol.UnitTests/Tools]
requirement: none
created: 2026-10-23
completed:
---
# BL-3380 — Hand-write the MCP tools/list and tools/call method handlers

## Goal

`Curl.Mcp.Protocol.UnitLibrary/Tools/` holds the `tools/list` and `tools/call` method handlers over the tool registry, with the same answers as the SDK server.

## Context

- MCP transport and message rules: https://modelcontextprotocol.io/specification (read the protocol revision the golden conversations recorded by BL-3374 show the SDK 2.2.0 server negotiating, and say which revision you read). Handlers implement `IMcpMethodHandler` from BL-3379; the registry and `IMcpTool` come from BL-3378. Each handler is its own file and contributes the `tools` capability.
- `tools/list` lists every registered tool in registration order with the golden shape of BL-3374 (compare after it is Done). `tools/call`: the tool is found by `name`; an unknown name, missing `name`, or `arguments` that is not a JSON object is a -32602 error or a tool error exactly as the golden files show; a tool that returns a failure result is passed through with `isError: true`; an exception thrown by a tool becomes a tool-error result (`isError: true`, a short message with no stack trace), not a protocol error, as the SDK does; the cancellation token reaches the tool.
- Pagination: the tool list fits one page; a `cursor` param is accepted and ignored, as the SDK server did for resources (see `CurlAiHelpResourceHandlers`).
- Tests use fake `IMcpTool` classes defined in the test project, one file each.

## Acceptance criteria

- [ ] `ToolsListHandler` and `ToolsCallHandler` exist under `Tools/`, one type per file, with XML doc comments.
- [ ] Tests cover list order, empty registry, unknown tool, missing name, non-object arguments, a tool returning an error result, a tool throwing, cancellation reaching the tool, and a cursor being ignored.
- [ ] Where BL-3374's golden files exist, a test asserts the handlers' output equals the golden `tools/list` and unknown-tool responses as JSON values for a registry holding equivalent fake tools; deliberate differences are listed in Notes.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Mcp.Protocol.UnitLibrary` reports 0 failing members (100% line, 100% branch, complexity at most 10, CRAP at most 30), measured from the fast tests only.
- [ ] `dotnet build` of every project named in touches is clean with `-warnaserror`, and `dotnet test --filter "TestCategory!=Integration"` passes for the test projects named in touches, with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 20212ea9 e2276c44 ebd0d829 bf223748 6d2cec38.
