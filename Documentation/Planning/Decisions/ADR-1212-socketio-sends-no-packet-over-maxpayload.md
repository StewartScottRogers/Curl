# ADR-1212: Socket.IO sends no packet over the open packet's maxPayload and ends with exit 55

- Status: Accepted
- Date: 2026-11-02
- Decided by Claude under Stewart's delegation (BL-4599)

## Context

Engine.IO v4's open packet carries `maxPayload`, the most bytes the server accepts in one packet; a server
closes the connection on a longer one. ADR-1035's `socketio://` session sent the `-d` event whatever its size.
Upstream curl has no Socket.IO, so there is no curl behaviour to match; the conformance case
(`SocketIo:EIO-MaxPayload-1`) asks only that no packet over `maxPayload` is sent and that the transfer fails.

## Decision

- On the open packet the session reads `maxPayload` as a whole non-negative number of bytes. A handshake
  without it (or with any other value) sets no limit.
- Every packet the client sends (the namespace connect, the event, acknowledgements and pongs) is measured as
  the UTF-8 bytes of the whole Engine.IO packet, its type character included, since over the WebSocket
  transport one packet is one message and that message is what the server bounds. A packet of exactly
  `maxPayload` bytes is sent.
- A longer packet is not sent and the transfer ends with exit 55 (`CURLE_SEND_ERROR`, "failed sending
  network data"), message `Socket.IO packet of <n> bytes is over the server's maxPayload of <m> bytes`:
  the data could not be sent, and no other exit code describes a send the peer forbids.

## Consequences

An oversized `-d` event fails before it reaches the server instead of having the server drop the
connection. The rule lives in `SocketIoSession`, pinned by `SocketIoSessionTests`.
