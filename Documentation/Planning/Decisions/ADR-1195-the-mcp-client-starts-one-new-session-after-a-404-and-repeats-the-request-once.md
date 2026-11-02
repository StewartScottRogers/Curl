# ADR-1195: The MCP client starts one new session after a 404 and repeats the request once

- Status: Accepted
- Date: 2026-11-02
- Task: BL-4537
- Decided by Claude under Stewart's delegation.

## Context

Streamable HTTP "Session Management" (Model Context Protocol 2025-06-18) says that when a client gets
HTTP 404 Not Found to a request carrying an `Mcp-Session-Id`, it MUST start a new session by sending a
new `InitializeRequest` without a session id. It does not say how often a client tries again, and real
curl has no MCP client to measure.

## Decision

`McpStreamableHttpTransport` answers a 404 to a request it sent with a session id by forgetting the
session id and the `MCP-Protocol-Version` and failing with `McpHttpFailure.SessionExpired`; the
connection is closed, as after every failed exchange. A 404 without a session id stays
`UnexpectedStatus`. `McpClientFlow` catches `SessionExpired` once per request: it runs `initialize`
and `notifications/initialized` again and repeats the request, with its original JSON-RPC id, in the
new session. A second 404 to the repeated request ends the transfer with exit 8, the exit every other
transport failure gives (ADR-1190).

## Consequences

A server that expires a session mid-transfer no longer fails the transfer, and a server that answers
404 to every session cannot loop the client. Conformance case McpClient:MCP20250618-StreamableHttp-8
passes.
