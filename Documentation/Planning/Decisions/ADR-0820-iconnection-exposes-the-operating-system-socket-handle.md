# ADR-0820: IConnection exposes the operating system socket handle it runs on

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3846)
- Relates to: [ADR-0599](ADR-0599-engine-multi-socket-action-and-callbacks.md), [ADR-0610](ADR-0610-socket-callbacks-over-opaque-connection-handles.md)

## Context

CGR-034 (`Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`): `curl_multi_socket_action` tells the
socket callback synthetic numbers from `0x40000000`, so an application's `poll()` or `WSAPoll` on them
fails. Real libcurl tells it the TCP (or QUIC UDP) socket's descriptor. ADR-0610 kept every operating
system socket inside `Curl.Networking`, and `IConnection` had no member that could give one, so the multi
handle (BL-3563) had nothing real to hand out.

## Decision

`IConnection` gains `nint? SocketHandle`, a default interface member answering `null`, like
`LocalEndPoint`: a connection on no socket (in memory, a test fake) gives none without code.

- `StreamConnection` gives `NetworkStream.Socket.Handle` when its stream is a `NetworkStream`, which is
  every TCP connection `TcpDialer` and `TcpPendingConnection` make (a file descriptor on Linux and macOS,
  a `SOCKET` on Windows), and `null` over any other stream.
- Every wrapping connection forwards its inner connection's: `SslStreamConnection`,
  `HandBuiltTlsConnection` and `EarlyDataTlsConnection` the plaintext one, `PooledConnection` the
  underlying one, `TcpIoTraceConnection` its inner one, `Http2ProxyTunnelConnection` the connection to the
  proxy, and the engine's `SocketCallbackConnection` its inner one. Through a proxy the handle is the
  socket to the proxy, as in libcurl.
- `EndPointRecordingConnector` and `PrerequestConnector` hand the connector's connection on unchanged,
  so they need nothing.
- The protocol handlers' own wrappers (`HttpPrefixedConnection` and the like) live inside a transfer and
  are never seen by the multi handle; they keep the default until something needs them.

The member is read-only and gives a number, never the `Socket` object, so nothing outside
`Curl.Networking` can read, write or close the socket; ADR-0610's "never an operating system socket" is
amended for this one member only. The `CURLOPT_OPENSOCKETFUNCTION`, `SOCKOPTFUNCTION` and
`CLOSESOCKETFUNCTION` numbers stay as ADR-0610 decided.

## Consequences

BL-3563 can tell the socket callback and `curl_multi_waitfds` the real descriptor of each running
transfer's connection. QUIC connections (`IMultiplexedConnection`) are not covered here; their UDP socket
is BL-3563's to expose if it needs it.
