# ADR-1027: The MCP client's wire format is strict JSON-RPC 2.0 over System.Text.Json

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4214)

## Context

BL-4159 adds an MCP client extension; BL-4214 is its first part, the JSON-RPC 2.0 messages a client needs to
initialize a session, list tools and call one (Model Context Protocol 2025-06-18: "Base Protocol > Overview",
"Lifecycle" and "Server Features > Tools"). Upstream curl has no MCP client, so the extension is tested against the
specification, not against curl (ADR-0999).

## Decision

- `Curl.Extended.Protocol.McpClient.UnitLibrary/Wire` writes messages with `Utf8JsonWriter` and reads them with
  `JsonDocument`, both BCL and AOT-safe with no serializer reflection.
- `McpRequestWriter` gives each request the next id, a JSON number starting at 1, and takes an id only when the
  request is written. `initialize` asks for protocol version `2025-06-18` and declares an empty `capabilities`
  object: the client offers no roots, sampling or elicitation. `tools/list` has no `params` for the first page and
  `params.cursor` for the next. `tools/call` takes its arguments as JSON text that must be one object.
- `McpResponseReader.ReadResponse` returns the response whose numeric id is the awaited one and `null` for any
  other message: a server request or notification (it has a `method`) or a response to another, string or absent
  id. A message that is not one JSON object with `"jsonrpc":"2.0"` throws `McpWireFormatException`; so does an
  awaited response with neither `result` nor `error`, or an error without an integer `code` and string `message`.
  2025-06-18 removed JSON-RPC batching, so an array is malformed.
- The typed readers keep what a client acts on: InitializeResult's `protocolVersion`; each tool's `name`,
  `description` and `inputSchema` (as JSON text) and the page's `nextCursor`; a CallToolResult's content items
  (type, text of a text item, the whole item as JSON) and `isError`, false when absent. Members a client does not
  act on are ignored, so a newer server's additions do not break it.

## Consequences

The transports (BL-4215, BL-4216) frame and carry these bytes; the handler (BL-4159) decides what to do with an
unsupported protocol version and how to print results.
