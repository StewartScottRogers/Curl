# ADR-0801: The tool and resource model of the hand-written MCP server

- Status: Accepted
- Date: 2026-10-23
- Task: BL-3378
- Decided by Claude under Stewart's delegation.

## Context

BL-3378 adds `Curl.Mcp.Protocol.UnitLibrary/Model/`, the seam the tool and resource port tasks
of ADR-0799 implement. Its wire shapes must match the golden conversations BL-3374 recorded from
the SDK 2.2.0 server: `tools/list` entries carry `name`, `description`, `inputSchema` and, for
`version`, `parse_url` and `build_url` only, `annotations` with `idempotentHint` then
`readOnlyHint`; `tools/call` results carry `content` text blocks and `isError`, written even when
`false`; `resources/list` entries carry `name`, `uri`, `description`, `mimeType`; `resources/read`
`contents` carry `uri`, `mimeType`, `text`.

## Decision

1. Each wire shape is a small class whose properties are declared in the golden files' order, so
   the source generator writes them in that order. Optional properties (`annotations`, each hint,
   a resource's `description` and `mimeType`) are omitted when null; `isError` is always written.
2. `McpToolAnnotations` holds only the two hints the server's tools set. A further hint is added
   when a tool needs it, not before.
3. `IMcpTool` supplies its input schema as raw JSON text. `McpServerRegistry.Add` parses it once
   and throws `ArgumentException` unless it is a JSON object whose `type` is the string `object`;
   invalid JSON is wrapped in the same exception. A duplicate tool name throws `ArgumentException`
   and leaves the first tool registered.
4. `IMcpResourceProvider` lists a fixed set of resources, read once when the provider is added,
   and reads one by URI, returning `null` for not found. A provider any of whose URIs is already
   registered, or listed twice, throws `ArgumentException` and adds none of its resources.
5. One `McpModelJsonContext` in `Model/` serializes the four results (`McpToolListResult`,
   `McpToolCallResult`, `McpResourceListResult`, `McpResourceReadResult`); it is never combined
   with `JsonRpcJsonContext` (ADR-0621).

## Consequences

The default JSON encoder escapes characters such as `'` as `'`, as the SDK's output may not;
the golden replay compares parsed JSON, and the final parity task settles any byte difference.
