---
id: BL-4392
title: Resolve HTTP/3 host names through the WireGuard tunnel, not the system resolver
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extension.WireGuard.UnitLibrary/Tunnel/WireGuardDatagramConnector.cs, Curl.Networking.UnitLibrary/DatagramConnectorUdpChannelOpener.cs, Curl.Extension.WireGuard.UnitTests/Tunnel]
requirement: none
created: 2026-10-31
completed:
---
# BL-4392 — Resolve HTTP/3 host names through the WireGuard tunnel, not the system resolver

## Goal

`curl --wireguard wg.conf --http3 https://name/` resolves the name through the tunnel's DNS server as TCP transfers do, never through the system resolver.

## Context

Shift-end review 2026-10-31 (code-reviewer). Curl.Console/CurlComposition.cs:528 now gives the QUIC dialer connectionHooks.DatagramConnectorOver(datagramConnector), but TcpConnector.ConnectMultiplexedAsync resolves the host with the system resolver first and Curl.Networking.UnitLibrary/DatagramConnectorUdpChannelOpener.cs:83 hands an IP literal to WireGuardDatagramConnector.OpenAsync, so ResolveThroughTunnelAsync (ADR-1161) never runs for QUIC. The DNS query leaks outside the tunnel, contrary to the class doc, and a name only the tunnel DNS knows fails with exit 6 over HTTP/3 while TCP works. Trace the path before fixing; the fix may belong in the opener (pass the host name through) or in the hook seam.

## Acceptance criteria

- [ ] A test in Curl.Extension.WireGuard.UnitTests/Tunnel shows an HTTP/3 transfer to a host name under --wireguard sends its DNS query through the tunnel and makes no system-resolver call.
- [ ] TCP and IP-literal QUIC behaviour under --wireguard is unchanged (the existing tests pass).
- [ ] dotnet build is clean, the fast tests pass and every changed library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-10-31: Created.
