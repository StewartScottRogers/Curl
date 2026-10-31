# ADR-1050: The socket-callback QUIC wrapper always carries datagrams

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4254)

## Context

ADR-1043 made `IDatagramCarryingConnection` an optional interface a caller tests an
`IMultiplexedConnection` for; `QuicConnection` implements it. When a handle sets a socket callback,
`SocketCallbackConnector` hands out its QUIC connection wrapped in `SocketCallbackMultiplexedConnection`,
which implemented only `IMultiplexedConnection`, so an extension testing the connection for datagrams
found none. The task offered two shapes: the wrapper always implements the interface, forwarding when
the inner connection carries datagrams and otherwise answering 0 and `InvalidOperationException`; or a
second wrapper type returned only over a datagram-carrying connection.

## Decision

`SocketCallbackMultiplexedConnection` always implements `IDatagramCarryingConnection`. Over an inner
connection that implements it, `MaxDatagramPayloadLength`, `SendDatagramAsync` and
`ReceiveDatagramAsync` forward to it. Over any other, `MaxDatagramPayloadLength` is 0 and both calls
throw `InvalidOperationException`, as `QuicConnection` does when the peer accepts no datagrams.

## Consequences

- Every connection the real network gives is a `QuicConnection`, which always implements the
  interface, so the wrapper answers exactly as the bare connection does; a caller cannot tell them apart.
- One wrapper type, no change to `SocketCallbackConnector`.
- A test fake without datagrams, wrapped, now claims the interface but answers as a peer that accepts
  none, which a caller that checks `MaxDatagramPayloadLength` (as ADR-1043 asks) already handles.
