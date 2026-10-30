---
id: BL-4355
title: WireGuard extension: carry UDP inside the tunnel so HTTP/3 runs through --wireguard
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4147, BL-4354]
touches: [Curl.Extension.WireGuard.UnitLibrary/IpStack/Udp, Curl.Extension.WireGuard.UnitTests/IpStack/Udp, Curl.Extension.WireGuard.UnitLibrary/Tunnel/WireGuardDatagramConnector.cs]
requirement: none
created: 2026-10-30
completed:
---
# BL-4355 — WireGuard extension: carry UDP inside the tunnel so HTTP/3 runs through --wireguard

## Goal

`curl --wireguard wg0.conf --http3-only https://10.0.0.1/` runs QUIC through the tunnel.

## Context

BL-4147 (ADR-1159) carries TCP alone and replaces curl's datagram connector with `WireGuardDatagramConnector`, which refuses every UDP socket (exit 4) so no QUIC leaves outside the tunnel. This task adds UDP (RFC 768, with the RFC 1071 checksum `InternetChecksum` already has) to the IP stack and a datagram connector that opens an `IDatagramChannel` through the tunnel. Depends on BL-4354 because both change `Tunnel/`.

## Acceptance criteria

- [ ] The hook's datagram connector opens a UDP channel inside the tunnel whose datagrams are sealed as transport data and checked against RFC 768 by unit tests with recorded bytes.
- [ ] An HTTP/3 transfer's QUIC datagrams travel through the tunnel in a unit test with a fake peer.
- [ ] The four quality gates hold by Measure-CodeQuality.ps1.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-30: Created.
