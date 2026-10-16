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
completed:
---
# BL-2474 — Engine: socket callbacks for QUIC connections (SOCK_DGRAM open-socket)

## Goal

With `CURLOPT_OPENSOCKETFUNCTION`, `SOCKOPTFUNCTION` or `CLOSESOCKETFUNCTION` set, an HTTP/3 (QUIC) connection of the managed engine calls them as a TCP one does, the open-socket callback seeing `SOCK_DGRAM` and `IPPROTO_UDP`.

## Context

ADR-0610 (BL-2418): `Curl.Engine`'s `SocketCallbackConnector` calls the socket callbacks around TCP connections only; its `ConnectMultiplexedAsync` and `ConnectMultiplexedSessionAsync` pass QUIC through untouched. curl 8.22.0's `cf_socket_open` runs the same callbacks for the UDP socket of `cf-ngtcp2`. Wrap the QUIC connect the same way (the session path must still let `PoolingConnector` share one session).

## Acceptance criteria

- [ ] `SocketCallbackConnectorTests` pins open, sockopt and close order and both refusals for a QUIC connect.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members.

## Notes

## Log

- 2026-10-16: Created.
