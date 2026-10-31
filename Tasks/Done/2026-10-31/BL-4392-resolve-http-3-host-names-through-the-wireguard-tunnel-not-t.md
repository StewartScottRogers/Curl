---
id: BL-4392
title: Resolve HTTP/3 host names through the WireGuard tunnel, not the system resolver
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extension.WireGuard.UnitLibrary/Tunnel/WireGuardTunnelDnsResolver.cs, Curl.Extension.WireGuard.UnitLibrary/WireGuardResolverHook.cs, Curl.Extension.WireGuard.UnitLibrary/WireGuardExtension.cs, Curl.Extension.Composition.UnitLibrary/ExtensionResolverHookCatalogue.cs, Curl.Extension.WireGuard.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-10-31
completed: 2026-10-31
---
# BL-4392 — Resolve HTTP/3 host names through the WireGuard tunnel, not the system resolver

## Goal

`curl --wireguard wg.conf --http3 https://name/` resolves the name through the tunnel's DNS server as TCP transfers do, never through the system resolver.

## Context

Shift-end review 2026-10-31 (code-reviewer). Curl.Console/CurlComposition.cs:528 now gives the QUIC dialer connectionHooks.DatagramConnectorOver(datagramConnector), but TcpConnector.ConnectMultiplexedAsync resolves the host with the system resolver first and Curl.Networking.UnitLibrary/DatagramConnectorUdpChannelOpener.cs:83 hands an IP literal to WireGuardDatagramConnector.OpenAsync, so ResolveThroughTunnelAsync (ADR-1161) never runs for QUIC. The DNS query leaks outside the tunnel, contrary to the class doc, and a name only the tunnel DNS knows fails with exit 6 over HTTP/3 while TCP works. Trace the path before fixing; the fix may belong in the opener (pass the host name through) or in the hook seam.

## Acceptance criteria

- [x] A test in Curl.Extension.WireGuard.UnitTests/Tunnel shows an HTTP/3 transfer to a host name under --wireguard sends its DNS query through the tunnel and makes no system-resolver call.
- [x] TCP and IP-literal QUIC behaviour under --wireguard is unchanged (the existing tests pass).
- [x] dotnet build is clean, the fast tests pass and every changed library keeps 100% line and branch coverage.

## Notes

Traced: curl's TcpConnector resolves before QUIC and the opener only sees an IP, so the fix is a resolver hook (ADR-1172) over the tunnel DNS, not the opener. touches widened to the hook, extension and catalogue files for that. Quality run on WireGuard and Composition libraries: 0 failing members. Linux/macOS: tests are platform-neutral.

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: 0658ce8f acf3d8d1 5a90b930.
- 2026-10-31: Doing -> Done. Resolver hook over tunnel DNS (ADR-1172)
