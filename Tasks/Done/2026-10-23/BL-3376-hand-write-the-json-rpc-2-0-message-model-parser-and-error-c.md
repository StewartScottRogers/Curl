---
id: BL-3376
title: Hand-write the JSON-RPC 2.0 message model, parser and error codes for the MCP server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3375]
touches: [Curl.Mcp.Protocol.UnitLibrary/JsonRpc, Curl.Mcp.Protocol.UnitTests/JsonRpc, Curl.Mcp.Protocol.UnitLibrary/README.md, Curl.Mcp.Protocol.UnitLibrary/CLAUDE.md]
model: opus
requirement: none
created: 2026-10-23
completed: 2026-10-23
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

- [x] The message model, parser and response writer exist under `Curl.Mcp.Protocol.UnitLibrary/JsonRpc/`, one type per file, with XML doc comments.
- [x] Tests in `Curl.Mcp.Protocol.UnitTests/JsonRpc/` cover each error code, string and numeric ids, notifications (no id), and malformed input never throwing.
- [x] `dotnet build Curl.Mcp.Protocol.UnitLibrary -warnaserror` reports no IL2026, IL3050 or other trim warning, and no reflection-based serialization is used.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Mcp.Protocol.UnitLibrary` reports 0 failing members (100% line, 100% branch, complexity at most 10, CRAP at most 30), measured from the fast tests only.
- [x] `dotnet build` of every project named in touches is clean with `-warnaserror`, and `dotnet test --filter "TestCategory!=Integration"` passes for the test projects named in touches, with no test deleted, skipped or weakened.

## Notes
- Plan (run directly rather than through the full /feature agents, to fit the run's budget): one type per file under `JsonRpc/`; `JsonRpcIncoming` is the base of `JsonRpcRequest`, `JsonRpcNotification` and `JsonRpcInvalidMessage`; ids are kept as `JsonElement` so they echo byte for byte; responses go through one serialization-only `JsonRpcJsonContext` (ADR-0621).
- BL-3374 (golden conversations) was still in Doing, so the rules follow the JSON-RPC 2.0 specification and MCP revision 2025-06-18 (no batching, id never null), from the specification as known, not re-fetched; recorded in ADR-0800, decided by Claude under Stewart's delegation. The golden comparison is left to the final parity task.
- Choices: `"params": null` is absent; last duplicate key wins; nesting past 64 is a parse error; UTF-8 is checked over the whole line first because `JsonDocument` validates a string's UTF-8 only when it is read (the first test run caught this).
- Added `Curl.Mcp.Protocol.UnitLibrary/README.md` and `CLAUDE.md` to touches: both said the project was an empty scaffold, now false. No other task in Doing on origin/work/dark-factory names them.
- Measured: Measure-CodeQuality -Library Curl.Mcp.Protocol.UnitLibrary: 100% line, 100% branch, 21 members, 0 failing, worst CRAP 8 (ReadMessage was complexity 16 at first; split into ReadMessage and ReadCall). 41 tests pass. `dotnet build Curl.Mcp.Protocol.UnitLibrary -warnaserror`: 0 warnings, no IL warning. CI on Linux and macOS: the tests are platform-neutral (no path, file system or OS text).

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 951ca66b bbbd4230 baeb00a8 bf223748 6d2cec38.
- 2026-10-23: Doing -> Done. Curl.Mcp.Protocol.UnitLibrary parses JSON-RPC 2.0 lines into requests, notifications or invalid messages with exact error codes, and writes success and error responses through a source-generated context
