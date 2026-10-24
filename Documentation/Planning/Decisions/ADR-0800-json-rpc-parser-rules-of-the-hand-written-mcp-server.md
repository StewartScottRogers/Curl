# ADR-0800: The JSON-RPC parser rules of the hand-written MCP server

- Status: Accepted
- Date: 2026-10-23
- Task: BL-3376
- Decided by Claude under Stewart's delegation.

## Context

BL-3376 adds the JSON-RPC 2.0 layer of `Curl.Mcp.Protocol.UnitLibrary` (ADR-0799):
`JsonRpcMessageParser` reads one stdio line, `JsonRpcResponseWriter` writes a response. The
golden conversations of BL-3374, which would show how the SDK 2.2.0 server answers malformed
input, were not recorded when this task ran, so the rules follow the JSON-RPC 2.0 specification
and the Model Context Protocol's message rules (revision 2025-06-18: no batching, a request id
is a string or a number and never null). The final parity task compares them with the golden
conversations.

## Decision

1. Input that is not UTF-8, not JSON, or nests deeper than 64 levels (the `JsonDocument`
   default) is `-32700 Parse error` with a null id. UTF-8 is checked over the whole line first,
   because `JsonDocument` checks a string's UTF-8 only when the string is read.
2. JSON that is not an object - a batch array included, since MCP removed batching - is
   `-32600 Invalid Request` with a null id.
3. An `id` that is null, a boolean, an object or an array is `-32600` with a null id. An id that
   is a string or a number is kept as its JSON value and echoed byte for byte, so a number too
   large for `long` is not rounded.
4. A missing or wrong `jsonrpc` (anything but the string `"2.0"`), a missing or non-string
   `method`, or `params` that is neither an object, an array nor null is `-32600`, echoing the id
   when one was read.
5. `"params": null` is treated as absent. Unknown members are ignored. A key given twice takes
   its last value (the `JsonDocument` default, and what JavaScript's `JSON.parse` does).
6. `params` and `result` travel as `JsonElement`, so the MCP layer decides their shapes.
   Responses are written through one source-generated, serialization-only
   `JsonRpcJsonContext`, never combined with another resolver (ADR-0621). The writer returns the
   line without its terminator; the transport adds the newline.

## Consequences

The parser never throws on malformed input. If the golden conversations show the SDK answering
any of these cases differently, the parity task changes the rule here and records it.
