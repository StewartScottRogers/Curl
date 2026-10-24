# ADR-0804 — The hand-written MCP server's `tools/list` and `tools/call` handlers

- Status: Accepted
- Date: 2026-10-23
- Task: BL-3380
- Decided by Claude under Stewart's delegation.

## Context

BL-3380 adds `ToolsListHandler` and `ToolsCallHandler` to `Curl.Mcp.Protocol.UnitLibrary/Tools/`
over `McpServerRegistry` (ADR-0801) and `IMcpMethodHandler` (ADR-0803, BL-3379). They must answer as
the SDK 2.2.0 server did in BL-3374's golden conversations, which negotiate protocol revision
2025-06-18. The golden files pin the `tools/list` result, the `tools` capability
(`{"listChanged":true}`), the unknown-tool error (-32602, `Unknown tool: '<name>'`) and the
tool-error text for a tool that throws (`An error occurred invoking '<name>'.`). They do not pin a
missing `name` or `arguments` that is not an object.

## Decision

- `ToolsListHandler` alone contributes the `tools` capability, `{"listChanged":true}` as the SDK
  announced. `McpInitializeHandler` writes one member per contributor, so two handlers giving
  `tools` would write the member twice.
- `tools/list` lists every registered tool in registration order on one page; a `cursor` is
  accepted and ignored, as the SDK server did for resources.
- `tools/call`: params that are not an object or have no `name` string are -32602 `The tools/call
  request needs a name string in its params.`; an unknown name is -32602 `Unknown tool: '<name>'`
  (golden); `arguments` absent or `null` is passed to the tool as `{}`; `arguments` of any other
  non-object kind is -32602 `The tools/call arguments must be a JSON object.`
- A tool's result, `isError` true or false, is passed through unchanged.
- An exception a tool throws becomes the tool-error result `An error occurred invoking '<name>'.`
  with no exception text, as the golden `tools-call-curl-missing-args.json` shows; an
  `OperationCanceledException` while the request's token is cancelled propagates, so the
  dispatcher treats it as the request's cancellation.

## Consequences

The handlers answer the golden `tools/list` and unknown-tool responses as JSON values for a
registry of equivalent fake tools (`Curl.Mcp.Protocol.UnitTests/Tools`). The two -32602 messages
the golden files do not pin are Curl's own wording; a client reads the code, not the message.
