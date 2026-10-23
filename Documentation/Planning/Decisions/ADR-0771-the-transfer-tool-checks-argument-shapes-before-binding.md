# ADR-0771: The `transfer` tool checks its arguments' JSON shapes before the SDK binds them

- Status: Accepted
- Date: 2026-10-22
- Decided by Claude under Stewart's delegation (BL-3188)

## Context

The MCP SDK binds a `tools/call` request's arguments to `CurlTransferTool.TransferAsync`'s typed
parameters before `McpTransferTool` sees them. A wrong JSON shape - `headers` as an object, `body`
as a string, `url` as a number - makes that binding throw, and the SDK answers with its bare
`An error occurred invoking 'transfer'.`, naming neither the field nor what it should be (BL-3171
item 6). Real curl has no counterpart: this is the MCP server's own contract, so there is nothing
to measure.

Taking every argument as a raw `JsonElement` would let the tool check shapes itself, but would
empty the tool's published input schema, which tells an agent each field's type and is pinned by
`CurlMcpHostTests`.

## Decision

- The typed parameters, and so the input schema, stay as they are.
- `CurlMcpHost` registers a call-tool request filter, `CurlTransferTool.RefuseWrongArgumentShapes`,
  that runs before binding. For a `transfer` call it asks
  `McpTransferRequestCheck.CheckArgumentShapes` about the raw arguments; the first argument of the
  wrong shape answers with the tool's usual `InvalidRequest` error,
  `Invalid <field>: it must be <shape>, not <what was sent>.`, as an error result, never reaching
  the binder. Any other call, or a call whose shapes are right, goes on unchanged.
- Shapes: `url`, `method`, `proxy` and `userAgent` a string; `timeoutSeconds` a number;
  `followRedirects` and `insecure` a boolean; `headers` an array of objects each with a string
  `name` and a string `value`; `body` an object with a string `content` and, when present, a
  boolean `isBase64`. `null` is accepted wherever the parameter is nullable or a string (the value
  checks that follow refuse a null URL or method by name); the two booleans refuse it, as the
  binder would. An argument the tool has no field for is left to the binder, which ignores it.

## Consequences

- An agent sending a wrong shape learns which field and which shape, and can correct the call.
- The shape table in `McpTransferRequestCheck` must change with `TransferAsync`'s parameters.
