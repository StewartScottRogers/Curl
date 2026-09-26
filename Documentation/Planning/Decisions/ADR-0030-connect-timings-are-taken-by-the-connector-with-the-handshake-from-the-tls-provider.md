# ADR-0030 — Connect timings are taken by the connector, with the handshake's end from the TLS provider

- **Status:** Accepted
- **Date:** 2026-09-26

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

## Context

BL-211 fills `ConnectTimings` (ADR-0015) and `ConnectResult.LocalEndPoint` in
`TcpConnector` and `SslStreamTlsProvider`. Three questions had no answer in the contract:

1. Where the local end point comes from. `IConnection` carries only `RemoteEndPoint`,
   and only `TcpDialer` sees the socket.
2. Which timestamp is `TlsHandshakeCompleted`. The connector alone knows when it began
   and when the TCP connect finished; the provider alone knows when the handshake ended.
3. Whether `NameResolved` is `null` for a literal address. `ConnectTimings` allows it,
   but `TcpConnector` hands every host, literal or not, to `IDnsResolver`.

Measured on 2026-09-26 with the reference build (curl 8.21.0 Schannel, Windows):
`curl -s -o NUL -w "%{time_namelookup}" http://127.0.0.1:1/` prints `0.000065`, so curl
reports a name-lookup time for a literal address too.

## Decision

1. `ITcpDialer.DialAsync` returns a `DialedTcpConnection`: the connection and the local
   end point of its socket. `TcpConnector` puts that end point in
   `ConnectResult.LocalEndPoint`. The remote end point stays where it already was, on
   `IConnection.RemoteEndPoint`, which `SslStreamConnection` passes through. Through an
   HTTP proxy both are the proxy connection's, as curl reports them.
2. `TcpConnector` takes `Started` when `ConnectAsync` begins, `NameResolved` when the
   resolver returns, and `Connected` when the dial completes or, through a proxy, when
   the tunnel is open. `SslStreamTlsProvider` takes a `TimeProvider` (by default
   `TimeProvider.System`) and reports the handshake's start as `Started` and
   `Connected` and its end as `TlsHandshakeCompleted`. The connector keeps only the
   provider's `TlsHandshakeCompleted`; when a provider reports no timings, the moment it
   returned stands in for it.
3. `NameResolved` is always set by `TcpConnector`, literal address or not, because
   the resolver always runs and curl reports a lookup time for a literal address.

## Consequences

- One clock: the provider's timestamps are only comparable with the connector's when
  both have the same `TimeProvider`, so the composition root must pass its
  `TimeProvider` to `SslStreamTlsProvider` (a follow-up in `Curl.Console`).
- A test dialer returns a `DialedTcpConnection`; no contract in
  `Curl.Protocol.Abstractions` changed.
