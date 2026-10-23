---
id: BL-3379
title: Hand-write the MCP server dispatcher with initialize, ping and notifications
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3376, BL-3377, BL-3378]
touches: [Curl.Mcp.Protocol.UnitLibrary/Server, Curl.Mcp.Protocol.UnitTests/Server]
model: opus
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3379 — Hand-write the MCP server dispatcher with initialize, ping and notifications

## Goal

`Curl.Mcp.Protocol.UnitLibrary/Server/` runs the server loop over an `IMcpTransport`: it reads messages, routes each request to the `IMcpMethodHandler` registered for its method, answers with a result or a JSON-RPC error, and implements the lifecycle itself: `initialize` (protocol version negotiation, `serverInfo`, capabilities built from the handlers present), `notifications/initialized`, `ping`, and `notifications/cancelled`.

## Context

- MCP transport and message rules: https://modelcontextprotocol.io/specification (read the protocol revision the golden conversations recorded by BL-3374 show the SDK 2.2.0 server negotiating, and say which revision you read). Behaviour must match the SDK 2.2.0 server's golden conversations from BL-3374: which protocol version is answered for an older, current and unknown client version, the `serverInfo` name and version (`Curl.Mcp` and the assembly version, see the BL-2486 notes: protocol 2025-06-18, version 0.1.0.0), the capabilities object (`tools` and `resources`, with the flags the SDK sets, e.g. `listChanged`), the answer to a request before `initialize`, an unknown method (-32601), a request with invalid `params` (-32602), malformed input (-32700 / -32600) and requests with no id.
- Method handlers are the extension point: `IMcpMethodHandler` (method name, async handle taking `params` and a cancellation token, returning a result `JsonElement` or throwing a typed `McpProtocolException` carrying a JSON-RPC code). Each method is one file and the server is built with an explicit handler list; the tasks for `tools/*` and `resources/*` (in `Curl.Mcp.Protocol.UnitLibrary/Tools/` and `/Resources/`) add handlers without editing this folder. Capabilities come from `ICapabilityContributor`-style members on the handlers, so a new handler needs no edit here.
- Requests run concurrently (a slow `tools/call` must not block `ping`); each request has its own `CancellationTokenSource`, cancelled by `notifications/cancelled` for that id, by transport end and by the host's token; a cancelled request sends no response, as the specification says. An exception escaping a handler becomes -32603 without the stack trace; the process never crashes on one bad message. The loop ends when the transport returns `null` or the token cancels, after in-flight requests finish or are cancelled.
- Time is injected (`TimeProvider`) if used at all; no `Thread.Sleep`. Tests drive the loop through an in-memory fake transport.

## Acceptance criteria

- [x] `McpServer`, `IMcpMethodHandler`, `McpProtocolException`, and the handlers for `initialize`, `ping`, `notifications/initialized` and `notifications/cancelled` exist under `Server/`, one type per file, with XML doc comments.
- [x] Tests cover protocol-version negotiation for an older, the current and an unknown client version, capabilities derived from the handlers present, a request before `initialize`, an unknown method, invalid params, concurrent requests (a blocked handler does not delay `ping`), cancellation by `notifications/cancelled`, transport end, handler exceptions becoming -32603 without leaking a stack trace.
- [x] Where the golden files of BL-3374 exist, a test reads the matching `initialize`, `ping` and error responses and asserts the server produces the same JSON values (copy the golden lines into the UnitTests project as test data if the file cannot be linked); any deliberate difference is listed in Notes.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Mcp.Protocol.UnitLibrary` reports 0 failing members (100% line, 100% branch, complexity at most 10, CRAP at most 30), measured from the fast tests only.
- [x] `dotnet build` of every project named in touches is clean with `-warnaserror`, and `dotnet test --filter "TestCategory!=Integration"` passes for the test projects named in touches, with no test deleted, skipped or weakened.

## Notes

- Delivered directly rather than through the full `/feature` subagent stages, to stay inside the run's cost cap; the decisions are in ADR-0803 (decided by Claude under Stewart's delegation). Revision read: 2025-11-25, the latest the golden files show the SDK 2.2.0 server negotiating.
- `Server/` holds `McpServer`, `IMcpMethodHandler`, `IMcpCapabilityContributor`, `McpProtocolException`, `McpServerInfo`, `McpRequestTracker` and the `initialize`, `ping`, `notifications/initialized` and `notifications/cancelled` handlers, one type per file. Notifications use `IMcpMethodHandler` too; their result is discarded.
- Golden: the `initialize-2024-11-05`, `-2025-06-18`, `-2025-11-25`, `-unknown-version`, `ping`, `unknown-method`, `request-before-initialize` (shape only), `malformed-json`, `non-object-message` and `batch-array` lines are copied into `McpServerTests` as test data and compared with `JsonElement.DeepEquals` (property order differs from the SDK, values do not). Deliberate difference: `logging: {}` appears in the capabilities only when a handler contributes it (the golden tests register one); no `logging/setLevel` handler exists yet.
- Measured: Measure-CodeQuality -Library Curl.Mcp.Protocol.UnitLibrary reports 100% line, 100% branch, 0 failing members. Curl.Mcp.Protocol.UnitTests 116 passed; build clean with -warnaserror; Curl.Mcp.UnitTests still builds. Tests are platform-neutral (no paths, OS text or certificates), so CI on Linux and macOS is covered by the same run.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: c9a7c46e 6b02da99 fed38e0b bf223748 6d2cec38.
- 2026-10-23: Doing -> Done. McpServer dispatches requests concurrently over IMcpTransport with initialize negotiation, ping and cancellation, matching the SDK golden answers
