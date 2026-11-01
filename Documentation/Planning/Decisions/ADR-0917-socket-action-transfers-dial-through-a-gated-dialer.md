# ADR-0917: A socket-action transfer dials through a gated dialer carried on its shared state

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3859)
- Relates to: [ADR-0914](ADR-0914-socket-action-transfers-wait-for-readiness-beneath-tls.md), [ADR-0599](ADR-0599-engine-multi-socket-action-and-callbacks.md), [ADR-0658](ADR-0658-libtest-engine-dials-the-in-memory-servers.md)

## Context

ADR-0914 built `TransferSocketWait` and `SocketActionGatedConnection` but routed neither: a transfer's
transports come from `ManagedCurlEngine.CreateTransports`, which builds them per perform from the handle's
options and its `ManagedEngineSharedState`, and the `TcpConnector` dials through the `ITcpDialer`
`EngineComposition.DialersOf` picks (the socket dialer, or the conformance harness's).

## Decision

- `ManagedEasyTransfer.SocketWait` (internal, `null` by default) is what the multi handle (BL-3563) sets on a
  transfer it drives by `curl_multi_socket_action`. Each perform calls its `Start` with the perform's options
  and runs over `PerformShared.WithSocketWait(SocketWait)`, a copy of the state carrying it
  (`ManagedEngineSharedState.SocketWait`); with none the state is unchanged.
- `ManagedCurlEngine.CreateTransports` passes it to `EngineComposition.CreateTransports`'s new optional
  `socketWait`, which hands the `TcpConnector` `SocketActionGatedTcpDialer.Over(dialer, socketWait)`: the
  dialer itself when `null`, so outside socket-action mode nothing changes, for sockets and the harness alike.
- `SocketActionGatedTcpDialer` wraps every dial (`DialAsync`, both `DialFromAsync`, `DialFromDeviceAsync`,
  `DialUnixSocketAsync`): `BeginConnect` before, `EndConnect` when it connects, and the connection wrapped
  in `SocketActionGatedConnection`. The `TcpConnector` lays TLS and any proxy tunnel over the dialed
  connection, so the gate sits beneath TLS as ADR-0914 asks. A failed dial leaves the transfer connecting,
  as libcurl stays in `OUT` while it tries the next address; with happy eyeballs the winning dial ends it.
- A pooled connection keeps the gate of the transfer that dialed it, as the multi handle signals by socket.
- The connect deadline ends when the TCP dial connects, not after the TLS handshake: the handshake's reads and
  writes are gated and wait `IN`/`OUT` like any other, and the transfer's own connect timeout still bounds it.
- The HTTP handler's expect-100 wait feeding `BeginExpectContinueWait`/`EndExpectContinueWait` needs a hook
  in `Curl.Original.Protocol.Http.UnitLibrary`, which BL-3859's `touches` does not name; it is its own task.

## Consequences

The routing lands before BL-3563 creates one `TransferSocketWait` per transfer, so until then no production
path sets `SocketWait` and nothing changes. Tests: `SocketActionGatedTcpDialerTests`.
