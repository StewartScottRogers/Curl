# ADR-0358 — A socket `connectx` left connecting is read and written through its own stream

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1158.

## Context

ADR-0355 point 2 expected the `dup` of a socket connected through `connectx` to read its peer
name and so report itself connected. CI's macOS job showed otherwise (run 37004026335):
`connectx` with `CONNECT_RESUME_ON_READ_WRITE` sends no SYN until the first write, so the socket
is still connecting, `getpeername` fails with `ENOTCONN`, and the new `Socket.Connected` is
`false`. `NetworkStream` refuses a socket that is not connected, so the dialer could not wrap it.
`Socket.Send` and `Socket.Receive` do not check `Connected`, and the kernel accepts the first
write on such a socket as the data that rides in the SYN.

## Decision

1. `TcpDialer` wraps a connected socket in a `NetworkStream`, as before, and a socket that is not
   yet connected - only the `connectx` route answers one - in the new
   `DeferredConnectSocketStream`, a minimal `Stream` over `Socket.Send` and `Socket.Receive` that
   owns the socket. It is unit tested on every platform over a loopback socket pair.
2. Waiting for the connect, or sending the SYN early so the socket reports connected, was not
   chosen: either sends the SYN without the request, which is what `--tcp-fastopen` avoids.
3. ADR-0355's other points stand; this replaces its expectation that the socket reads as connected.

## Consequences

`--tcp-fastopen` on macOS dials without the `NetworkStream` refusal, and the first request still
leaves with the SYN. The macOS test pins the route through the new stream.
