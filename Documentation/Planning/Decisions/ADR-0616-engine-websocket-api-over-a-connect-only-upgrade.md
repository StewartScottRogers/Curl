# ADR-0616 — The engine's WebSocket API runs over a CONNECT_ONLY 2 upgrade it holds itself

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2443.

## Context

`IEasyWebSocket` (`curl_ws_recv`, `curl_ws_send`, `curl_ws_meta`, `curl_ws_start_frame`) threw
`NotSupportedException` on `ManagedEasyTransfer`. BL-2471 gave `Curl.Original.Protocol.Ws` an upgrade that stops after the
`101` and hands the connection back (`WsProtocolHandler.ConnectAndUpgradeOnlyAsync`), a public frame encoder and a
decoder that reports each run of payload with its opcode, FIN bit, offset and bytes left. BL-2423 gave the engine a
`CURLOPT_CONNECT_ONLY` 1 connection of its own. What was left: how a `CONNECT_ONLY` 2 perform reaches the upgrade,
and how libcurl's frame calls behave over it. libcurl cannot be run from this repository's tests, so the behaviour
below is read from curl 8.22.0's `lib/ws.c`, not measured.

## Decision

1. **A `CONNECT_ONLY` 2 perform of a `ws` or `wss` URL runs the normal perform path** - setup, context, follower,
   cleanup, `Info` - over a dispatcher of one `WebSocketUpgradeOnlyHandler`, which calls
   `ConnectAndUpgradeOnlyAsync` and gives the open connection and the bytes that came with the reply head to the
   handle's `ManagedEasyWebSocket`. Its `WsProtocolHandler` dials through `CreateConnectOnlyConnector` (the
   transports' `TcpConnector`, never the pool, as ADR-0608), so the connection is the handle's alone, and
   authenticates with `ManagedCurlEngine.CreateWebSocketAuthenticator`, the ranked schemes the composition gives the
   WebSocket handler. A refused upgrade answers the transfer's code (22 for a non-`101`) and leaves no connection.
2. **`CONNECT_ONLY` 2 on any other scheme only connects**, as `CONNECT_ONLY` 1: libcurl upgrades only WebSocket URLs.
3. **Receive never waits**, like ADR-0608's `curl_easy_recv`: one read at a time, `CURLE_AGAIN` while it runs or
   while only part of a frame has come; then the first waiting run, as much as the buffer holds, with
   `struct curl_ws_frame`'s `flags`, `offset` (bytes of the frame before these), `bytesleft` (bytes of the frame
   still to come) and `len`. A continuation fragment reports its message's `CURLWS_TEXT` or `CURLWS_BINARY`, and
   `CURLWS_CONT` is set on every frame whose FIN bit is clear, as `ws_frame_firstbyte2flags`. The server closing is
   `CURLE_GOT_NOTHING`, a failed read or a protocol violation `CURLE_RECV_ERROR` (after the frames decoded before
   it), and the stream stays failed.
4. **Auto-pong**: unless `CURLWS_NOAUTOPONG` is set, each completed ping is answered with a pong carrying its
   payload, and the ping is still handed to the application. A pong that cannot be written fails the stream with
   `CURLE_SEND_ERROR`.
5. **Send** writes one masked frame per call, the opcode chosen as `ws_frame_flags2firstbyte` does
   (`WebSocketFrameFlags.TryFrameOf`): `CURLWS_CONT` clears FIN; a data bit starts a message, or continues the one in
   progress; no bit, or `CURLWS_CONT` alone, continues the message in progress; control frames are never
   fragmented; anything else is `CURLE_BAD_FUNCTION_ARGUMENT`. A failed write is `CURLE_SEND_ERROR`.
6. **`curl_ws_meta`** returns the frame of the last data handed over, as `lib/ws.c` returns `ws->recvframe`, and
   `null` before any since the upgrade. `Curl.Api`'s doc comment says `null` outside the write callback; correcting
   it is BL-2481's.
7. **Codes outside a WebSocket**: with the options no longer connect-only, `CURLE_UNSUPPORTED_PROTOCOL` (libcurl's
   "CONNECT_ONLY is required"); connect-only with no upgraded connection, `CURLE_BAD_FUNCTION_ARGUMENT`.
8. **`CURLWS_OFFSET` and `curl_ws_start_frame` answer `CURLE_BAD_FUNCTION_ARGUMENT` until BL-2481**, which needs a
   partial-frame encoder in `Curl.Original.Protocol.Ws` first. Every perform and cleanup close the upgraded connection;
   `Reset` keeps it, as for ADR-0608.

## Consequences

`Libcurl` and `Curl.Api` can drive `curl_ws_recv` and `curl_ws_send` against a real server. The frame masks and
`Sec-WebSocket-Key` come from the internal `ManagedEasyTransfer.WebSocketRandomSource`, which tests fix.
