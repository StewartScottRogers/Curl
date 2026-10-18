---
id: BL-2474
title: Engine: socket callbacks for QUIC connections (SOCK_DGRAM open-socket)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2474 — Engine: socket callbacks for QUIC connections (SOCK_DGRAM open-socket)

## Goal

With `CURLOPT_OPENSOCKETFUNCTION`, `SOCKOPTFUNCTION` or `CLOSESOCKETFUNCTION` set, an HTTP/3 (QUIC) connection of the managed engine calls them as a TCP one does, the open-socket callback seeing `SOCK_DGRAM` and `IPPROTO_UDP`.

## Context

ADR-0610 (BL-2418): `Curl.Engine`'s `SocketCallbackConnector` calls the socket callbacks around TCP connections only; its `ConnectMultiplexedAsync` and `ConnectMultiplexedSessionAsync` pass QUIC through untouched. curl 8.22.0's `cf_socket_open` runs the same callbacks for the UDP socket of `cf-ngtcp2`. Wrap the QUIC connect the same way (the session path must still let `PoolingConnector` share one session).

## Acceptance criteria

- [x] `SocketCallbackConnectorTests` pins open, sockopt and close order and both refusals for a QUIC connect.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members.

## Notes

- `ConnectMultiplexedAsync` now runs the same open-socket and sockopt steps as `ConnectAsync` (shared `OpenSocketAsync`), with `DatagramAddressOf` giving `SOCK_DGRAM` 2 and `IPPROTO_UDP` 17. An accepted connection becomes a new `SocketCallbackMultiplexedConnection`, which calls close-socket once on dispose.
- `ConnectMultiplexedSessionAsync` builds the session over that wrapped connect (`ToConnectResult`). The `PoolingConnector` sits above this connector (`PoolOf`), so it still shares one session and the callbacks run once per QUIC connection. Recorded in ADR-0660.
- The refusals reuse the TCP message `Failed to connect to <host>:<port> after <n> ms: <text>` with exit 7 and 42, as curl's `cf_socket_open` is the same function for both.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members. Fast tests are all green.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: cb168e42 c4aff572.
- 2026-10-16: Doing -> Done. HTTP/3 (QUIC) connections now call the open-socket, sockopt and close-socket callbacks, open-socket seeing SOCK_DGRAM/IPPROTO_UDP
