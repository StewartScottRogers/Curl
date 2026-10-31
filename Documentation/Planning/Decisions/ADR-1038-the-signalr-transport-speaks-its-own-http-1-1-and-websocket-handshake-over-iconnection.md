# ADR-1038 — The SignalR transport speaks its own HTTP/1.1 and WebSocket handshake over IConnection

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4219.

## Context

The SignalR extension (ADR-0999, ADR-1032) needs the negotiate request and the WebSocket transport
(TransportProtocols.md). An extension may not reference `Curl.Protocol.Http` or `Curl.Protocol.Ws`, so it needs
its own HTTP/1.1 exchange and RFC 6455 client handshake. It may reference the shared frame codec
`Curl.WebSocket.UnitLibrary`. The specification leaves some choices to the client.

## Decision

- `Transport/` writes HTTP/1.1 requests with no body and reads responses with `Content-Length`,
  `Transfer-Encoding: chunked` or read-to-close bodies. Lines are capped at 64 KiB.
- The negotiate request is `POST <negotiate URL> HTTP/1.1` with `Host`, an `Authorization: Bearer` field after
  a redirect, and `Content-Length: 0`. Each negotiate exchange uses its own connection, which is closed afterwards.
  The connection comes from an injected opener, so a redirect to another host gets its own connection.
- A redirect is followed **once**, as the task asks. A second redirect, a redirect URL that is not absolute, a
  non-2xx status, an `error` field, or a response that offers no `WebSockets` transport fails with
  `SignalRTransportException`. A body that is not JSON fails with `SignalRWireFormatException`.
- The access token goes on the WebSocket upgrade as `Authorization: Bearer`, as the .NET client does outside a
  browser. It does not go in the `access_token` query parameter, which browsers use only because they cannot set
  headers.
- The upgrade is accepted only when the response is a `101` with `Upgrade: websocket`, a `Connection` field that
  holds `Upgrade`, and the right `Sec-WebSocket-Accept` (RFC 6455 section 4.1).
- After the upgrade:
  - Text and continuation frames feed the hub record splitter.
  - The last Ping in each read is answered with a Pong that carries its payload (RFC 6455 section 5.5.2 allows
    answering only the most recent).
  - A Close is answered with a Close that carries its status code. After that the session gives the records it
    already has, then `null`.
  - Binary and Pong frames are ignored.

## Consequences

BL-4108's handler only opens connections, wires the negotiator, the upgrade and the session to a URL, and adds the
option. No socket is opened in tests: every test drives a scripted `IConnection`.
