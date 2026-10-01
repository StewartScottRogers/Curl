---
id: BL-1051
title: Report the QUIC socket's real local address in -v's Established connection line
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-734]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-30
completed:
---
# BL-1051 — Report the QUIC socket's real local address in -v's Established connection line

## Goal

`curl -v --http3-only https://<host>/` writes `Established connection to <host> (<ip> port 443) from <local ip> port <local port> ` with the interface address the QUIC datagrams leave from, as curl does, not `0.0.0.0`, and `%{local_ip}` reports the same address.

## Context

- Measured by BL-734 (curl.se's 8.18.0 ngtcp2 build, `-v --http3-only https://cloudflare-quic.com/`): `* Established connection to cloudflare-quic.com (104.18.26.14 port 443) from 192.168.1.174 port 51486 `. Curl writes `from 0.0.0.0 port 57486 `: `QuicDialer.Connected` (`Curl.Networking.UnitLibrary/QuicDialer.cs`) takes `channel.LocalEndPoint`, which for a UDP socket bound to the wildcard address and never connected is `0.0.0.0`. curl's `cf_socket` connects its UDP socket to the peer, so the kernel picks the source address and `getsockname` returns it.
- Likely fix: connect the UDP channel's socket to the peer (or ask the kernel for the route's source address) before reading `LocalEndPoint`; the `IDatagramChannel` seam and `UdpDatagramChannel` are in `Curl.Networking.UnitLibrary`.

## Acceptance criteria

- [ ] A test in `Curl.Networking.UnitTests` shows the `ConnectionOpenedEvent` a QUIC connect reports carries a non-wildcard local address (a loopback peer gives `127.0.0.1` or `::1`).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking.UnitLibrary`.

## Notes

## Log

- 2026-09-30: Created.
