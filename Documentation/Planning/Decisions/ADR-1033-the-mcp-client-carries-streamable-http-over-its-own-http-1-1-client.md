# ADR-1033: The MCP client carries Streamable HTTP over its own HTTP/1.1 client

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4215)

## Context

The McpClient extension sends MCP messages over the Streamable HTTP transport (Model Context Protocol
2025-06-18, "Base Protocol > Transports > Streamable HTTP"). An extension may not reference
`Curl.Original.Protocol.Http` or another extension (ADR-0999), so it cannot borrow curl's HTTP stack or
`Curl.Extended.Protocol.S3`'s small HTTP/1.1 client.

## Decision

`Curl.Extended.Protocol.McpClient.UnitLibrary/StreamableHttp/` holds its own minimal HTTP/1.1 client, adapted from
`Curl.Extended.Protocol.S3`'s: request line and headers, Content-Length, chunked and close-delimited response
bodies (RFC 9112 §6.3), interim 1xx responses skipped, and the connection kept only when a response was
read to its end and leaves it open. `McpStreamableHttpTransport` sits on it:

- It takes a `Func<CancellationToken, ValueTask<IConnection>>` rather than the engine's `IConnector` and
  `TlsProvider`, so the handler that composes those (TCP, then TLS for an `https` endpoint) is the only
  place that knows them, and the transport is tested with recorded bytes alone.
- Every request is a POST with `Accept: application/json, text/event-stream` and `Content-Type:
  application/json`. A request needs 200 and a reply of either type; a notification (or a response to a
  server's request) needs 202 Accepted. Any other status fails with `McpHttpException` carrying
  `McpHttpFailure.UnexpectedStatus` and the status, so the caller can tell a 404 (the session ended) apart.
- A `text/event-stream` reply is read until the awaited response's event; the server's own requests and
  notifications on it are skipped, and the connection is then closed rather than drained, since a server
  may keep the stream open.
- `Mcp-Session-Id` is taken from any response that carries one and must be visible ASCII; it and
  `MCP-Protocol-Version` (which the caller sets once initialization settles it) go on every later request.
  `EndSessionAsync` sends DELETE and counts 2xx or 405 (the server not letting the client end it) as ended.
- A stale kept connection is not retried; the exchange fails with `ClosedEarly`.

The connection is closed without a `try` block holding an `await` (`CloseConnectionUnlessKeptAsync`
awaits the exchange with `ConfigureAwaitOptions.SuppressThrowing`), because coverlet counts the
compiler's unreachable non-`Exception` rethrow branch of an `await` in `finally` as uncovered.

## Consequences

The extension holds a second small HTTP/1.1 client beside S3's; each stays within its own extension. A
server that answers on a stream through a GET (server-initiated messages), resumes a stream with
`Last-Event-ID`, or wants the server's own requests answered on a POST's event stream needs later work.
