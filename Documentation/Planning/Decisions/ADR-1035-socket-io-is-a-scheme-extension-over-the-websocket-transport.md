# ADR-1035 — Socket.IO is a scheme extension over the WebSocket transport

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4109.

## Context

BL-4109 adds Socket.IO as an extension (ADR-0999), `Curl.Extension.Protocol.SocketIo.UnitLibrary`. Upstream
curl has no Socket.IO support, so the specifications decide: the Socket.IO protocol v5
(socket.io/docs/v4/socket-io-protocol) and the Engine.IO protocol v4
(socket.io/docs/v4/engine-io-protocol), as published in 2026. An extension references no
`Curl.Protocol.*` library, so it cannot reuse `Curl.Protocol.Ws`; it may reference the shared frame
codec `Curl.WebSocket.UnitLibrary` (ADR-1017).

## Decision

- Schemes `socketio://host[:port]/namespace[?query]` and `socketios://` (TLS through curl's connector).
  Default ports 80 and 443, as the reference client's `http`/`https` URLs. It is the first extension
  registered in `ExtensionCatalogue.cs` as a protocol handler; with `CURL_EXTENSIONS=0` no handler
  serves the schemes, so curl's dispatcher refuses them as any unknown scheme: exit 1,
  `Protocol "socketio" not supported`.
- As the reference JavaScript client reads `io("http://host/chat")`: the URL's path is the namespace
  (`/` when empty) and the Engine.IO path is always `/socket.io/`. The URL's query is appended to the
  handshake query `EIO=4&transport=websocket`, so a server's own query parameters (a token) reach it.
- Transport: WebSocket only, connected directly (Engine.IO v4 "Transports" lets a client start on
  WebSocket without HTTP long-polling). The upgrade is RFC 6455 section 4, the `Sec-WebSocket-Accept`
  checked; client frames come from `WsFrameEncoder`, server frames are read with `WsFrameDecoder`.
  Each WebSocket ping is answered with a pong, and each Engine.IO `ping` with a `pong` echoing its
  payload.
- Emit: the `-d` data must be a JSON array whose first element is a string, the event name; it is sent
  as one `EVENT` packet once the server confirms the namespace (`CONNECT`). Other data fails with exit 2
  before connecting. Without `-d` the transfer only receives.
- Receive: each `EVENT` packet on the namespace is written to the output as its JSON array and `\n`;
  an event that asks for an acknowledgement is acknowledged with `[]`. The transfer ends with exit 0 on
  `DISCONNECT`, an Engine.IO `close`, a WebSocket close frame or the connection closing, after the
  namespace connected; `--max-time` bounds a server that never ends it.
- Failures: a refused upgrade is exit 22 `Refused WebSocket upgrade: <status>` (curl's text for `ws://`),
  a `CONNECT_ERROR` exit 22 with the server's data, nothing before the reply head exit 52
  `Empty reply from server`, closing before the namespace connected exit 52, a wrong accept or
  malformed head exit 8, a frame violation exit 56 with the shared decoder's message.

## Left out, and why

- HTTP long-polling and the upgrade from it: every current Socket.IO server serves WebSocket directly
  unless configured to refuse it; polling would need an HTTP client the extension may not reference.
- Binary events and acknowledgements (`BINARY_EVENT`, `BINARY_ACK`) and their attachments: a command
  line has no way to give or show the binary parts; such packets are ignored.
- A `CONNECT` auth payload, several emitted events and a ping timeout of our own: none has a command-line
  form yet; each can come as an extension option under its own task.
