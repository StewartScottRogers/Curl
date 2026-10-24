# ADR-0803: The dispatcher rules of the hand-written MCP server

- Status: Accepted
- Date: 2026-10-23
- Task: BL-3379
- Decided by Claude under Stewart's delegation.

## Context

BL-3379 adds `McpServer` and its lifecycle handlers to `Curl.Mcp.Protocol.UnitLibrary/Server/`
(ADR-0799). The server must answer as the SDK 2.2.0 server did in the golden conversations of
BL-3374. Those show protocol revisions 2024-11-05, 2025-06-18 and 2025-11-25 echoed back and an
unknown revision answered with 2025-11-25, so the revision read is 2025-11-25 (its lifecycle,
ping and cancellation sections). They also show a request before `initialize` answered normally,
`Method '<name>' is not available.` for an unknown method (-32601), and no answer at all to
malformed JSON, a non-object message or a batch array.

## Decision

1. `initialize` echoes a client revision in 2024-11-05, 2025-03-26, 2025-06-18 or 2025-11-25 and
   answers 2025-11-25 to any other. Params without a `protocolVersion` string are -32602.
2. The server does not gate requests on `initialize` or `notifications/initialized`: the SDK
   answered a `tools/list` sent first, and a drop-in replacement answers the same.
3. Capabilities are the `IMcpCapabilityContributor` members of the handlers the server is built
   with, ordered by name. The SDK listed `logging: {}`; this server lists it only when a handler
   for `logging/setLevel` contributes it, so the host decides (a deliberate difference until such
   a handler exists).
4. An invalid message is answered only when it carries an id (-32600 or -32700 for that id); one
   without an id is dropped, as the SDK dropped malformed JSON, non-objects and batches.
5. Every request runs on its own task with its own cancellation source, linked to the host's
   token and cancelled by `notifications/cancelled` naming its id (matched by the id's raw JSON
   text, so `1` and `"1"` differ), by the transport's end and by the host. A cancelled request
   sends nothing. A `McpProtocolException` becomes its code and message; any other exception
   becomes -32603 `An error occurred.` with no detail. Notifications run in order on the read
   loop; their failures are swallowed. Responses are written one at a time under a lock.
6. A read that is cancelled ends the loop as the transport's end does; any other read failure is
   rethrown by `RunAsync` once the requests in flight have been cancelled and have finished.

## Consequences

The `tools/*` and `resources/*` handlers are added in their own folders and passed to the
`McpServer` constructor; nothing in `Server/` changes when one is added.
