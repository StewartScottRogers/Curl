# ADR-0660 — The socket callbacks run for QUIC connections too, over `SOCK_DGRAM` and `IPPROTO_UDP`

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2474.

## Context

ADR-0610 (BL-2418) put `SocketCallbackConnector` under the engine's pool so that
`CURLOPT_OPENSOCKETFUNCTION`, `CURLOPT_SOCKOPTFUNCTION` and `CURLOPT_CLOSESOCKETFUNCTION` run around each TCP
connection, and let QUIC connections pass through untouched. In curl 8.22.0 `cf-ngtcp2` opens its UDP socket with
the same `cf_socket_open` as TCP, so an HTTP/3 transfer calls the same three callbacks, the `struct curl_sockaddr`
carrying `SOCK_DGRAM` and `IPPROTO_UDP`, with the same `CURLE_COULDNT_CONNECT` and `CURLE_ABORTED_BY_CALLBACK`
refusals and `Curl_socket_close` calling the close-socket callback.

## Decision

- `SocketCallbackConnector.ConnectMultiplexedAsync` connects through the inner connector, then runs the same
  steps as `ConnectAsync`: the open-socket callback with `CurlSocketType.IpConnection` and
  `DatagramAddressOf(peer)` (`SOCK_DGRAM` 2, `IPPROTO_UDP` 17, the family and address bytes as for TCP), else the
  engine's next `CurlSocketHandles` number; `CurlSocket.Bad` disposes the QUIC connection and fails with exit 7;
  the socket-option callback's `Error` disposes it, calls close-socket and fails with exit 42; both with the
  `Failed to connect to <host>:<port> after <n> ms: <text>` message the TCP refusals give.
- An accepted connection is a `SocketCallbackMultiplexedConnection`, which forwards every member (the stream
  limit and peer idle timeout included) and calls close-socket once when disposed.
- `ConnectMultiplexedSessionAsync` builds the session over `ConnectMultiplexedAsync`'s result
  (`MultiplexedConnectResult.ToConnectResult`), which is what the inner connectors it wraps (the `TcpConnector`
  and FTP's data connector) do by default. The `PoolingConnector` sits above the `SocketCallbackConnector`, so it
  still shares the one session, and the callbacks run once per QUIC connection, not once per transfer.

## Consequences

An HTTP/3 transfer of a `Libcurl` or `Curl.Api` handle with a socket callback set sees its UDP socket as libcurl
would. A connector beneath the `SocketCallbackConnector` that overrides `ConnectMultiplexedSessionAsync` would have
that override bypassed; none does today.
