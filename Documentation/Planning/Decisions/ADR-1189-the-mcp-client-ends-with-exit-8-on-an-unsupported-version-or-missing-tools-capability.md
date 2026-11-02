# ADR-1189: The MCP client ends with exit 8 on an unsupported version or a missing tools capability

- Status: Accepted
- Date: 2026-11-02
- Task: BL-4482
- Decided by Claude under Stewart's delegation.

## Context

The Model Context Protocol 2025-06-18 says a client that does not support the protocol version in
the server's `initialize` result SHOULD disconnect ("Lifecycle > Version Negotiation"), and that both
parties MUST use only capabilities that were successfully negotiated ("Lifecycle > Operation"). It
does not say what a command-line client reports. Curl's `mcp://`, `mcps://` and `mcp+stdio://`
client implements only revision 2025-06-18 and, today, accepts any version and calls `tools/list`
or `tools/call` whatever capabilities the server declares (BL-4482's conformance cases).

## Decision

- Curl supports exactly the versions it implements, today only 2025-06-18. Any other version in the
  `initialize` result, older revisions included, is unsupported: curl sends no
  `notifications/initialized` and no further request (ending an HTTP session with DELETE is allowed)
  and exits 8 (`CURLE_WEIRD_SERVER_REPLY`), the code the client already uses for a server that breaks
  the protocol.
- A server whose `initialize` result declares no `tools` capability offers nothing curl can do:
  curl sends no `tools/` request and exits 8.
- The conformance cases pin this; BL-4514 and BL-4515 make the client do it.

## Consequences

A server that only speaks 2025-03-26 is refused until Curl implements that revision, which a task
adding it records in `McpRequestWriter` and here. Scripts see one exit code for every server that
cannot serve the session.
