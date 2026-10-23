---
id: BL-3376
title: Hand-write the JSON-RPC 2.0 message model, parser and error codes for the MCP server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3375]
touches: [Curl.Mcp.Protocol.UnitLibrary/JsonRpc, Curl.Mcp.Protocol.UnitTests/JsonRpc]
model: opus
requirement: none
created: 2026-10-23
completed:
---
# BL-3376 — Hand-write the JSON-RPC 2.0 message model, parser and error codes for the MCP server

## Goal

`Curl.Mcp.Protocol.UnitLibrary/JsonRpc/` holds the JSON-RPC 2.0 message model (request, notification, success response, error response, request id as string or number), the standard error codes plus MCP's resource-not-found code, a parser for one incoming line and a response writer, all on a source-generated `System.Text.Json` context.

## Context

- MCP transport and message rules: https://modelcontextprotocol.io/specification (read the protocol revision the golden conversations recorded by BL-3374 show the SDK 2.2.0 server negotiating, and say which revision you read). JSON-RPC 2.0: https://www.jsonrpc.org/specification.
- Design: `JsonRpcMessageParser` turns one line of UTF-8 into a `JsonRpcIncoming` (request, notification, or invalid with the exact code, -32700 parse error or -32600 invalid request, and the id when recoverable); `JsonRpcResponseWriter` builds success and error response bytes. `params` and `result` travel as `JsonElement` so later layers decide their shapes. Codes: -32700, -32600, -32601, -32602, -32603 and -32002 (resource not found).
- Match what the SDK answered in the golden conversations of BL-3374 (malformed JSON, non-object message, missing method, id echo for string and number ids) when that task is Done; otherwise follow the JSON-RPC specification and leave the golden comparison to the final task.
- Source-generated `JsonSerializerContext` only, in its own file; one context, never `JsonTypeInfoResolver.Combine` (ADR-0621). One type per file.
- Tests in `Curl.Mcp.Protocol.UnitTests/JsonRpc/`: every error code path, id types, null params, unknown properties ignored, duplicate keys, deep nesting, huge numeric ids, input that is not UTF-8.

## Acceptance criteria

- [ ] The message model, parser and response writer exist under `Curl.Mcp.Protocol.UnitLibrary/JsonRpc/`, one type per file, with XML doc comments.
- [ ] Tests in `Curl.Mcp.Protocol.UnitTests/JsonRpc/` cover each error code, string and numeric ids, notifications (no id), and malformed input never throwing.
- [ ] `dotnet build Curl.Mcp.Protocol.UnitLibrary -warnaserror` reports no IL2026, IL3050 or other trim warning, and no reflection-based serialization is used.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Mcp.Protocol.UnitLibrary` reports 0 failing members (100% line, 100% branch, complexity at most 10, CRAP at most 30), measured from the fast tests only.
- [ ] `dotnet build` of every project named in touches is clean with `-warnaserror`, and `dotnet test --filter "TestCategory!=Integration"` passes for the test projects named in touches, with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-23: Created.
