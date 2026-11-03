# ADR-1209: Socket.IO ends with exit 56 when no ping arrives within pingInterval + pingTimeout

- Status: Accepted
- Date: 2026-11-02
- Decided by Claude under Stewart's delegation (BL-4598)

## Context

Engine.IO v4's "Heartbeat" says the client considers the connection closed when the server's `ping` (`2`)
does not arrive within the open packet's `pingInterval` + `pingTimeout`. ADR-1035's `socketio://` session
ignored both handshake fields and waited until `-m`, so a dead server ran the transfer to exit 28. Upstream
curl has no Socket.IO, so there is no curl behaviour to match; the conformance case
(`SocketIo:EIO-Heartbeat-2`) asks only that the run end on the heartbeat, before `-m` and not with exit 28.

## Decision

- On the open packet the session reads `pingInterval` and `pingTimeout` (whole non-negative milliseconds) and
  waits at most their sum for the next ping, timed on the transfer's injected `TimeProvider`; each ping
  restarts the wait. A handshake without both as whole non-negative numbers sets no heartbeat (the server
  gave no promise to keep). The sum is capped at the longest a timer can wait (2^32 - 2 ms).
- A missed ping ends the transfer with exit 56 (`CURLE_RECV_ERROR`, "failure when receiving network data"),
  message `Socket.IO server sent no ping within <n> ms`: the connection is considered closed by the peer,
  which is a receive failure, while exit 28 stays reserved for curl's own `-m` and `--connect-timeout`.
- Cancellation of the transfer itself (`-m`) still propagates as cancellation, never as a missed ping.

## Consequences

A Socket.IO transfer against a server that stops pinging ends after `pingInterval` + `pingTimeout` with exit 56
instead of hanging until `-m`. The rule lives in `SocketIoSession`, pinned by `SocketIoSessionTests`.
