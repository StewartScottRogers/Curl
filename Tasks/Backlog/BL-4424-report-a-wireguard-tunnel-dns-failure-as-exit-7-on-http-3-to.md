---
id: BL-4424
title: Report a WireGuard tunnel DNS failure as exit 7 on HTTP/3 too, and on every tunnel failure in the DNS exchange
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extension.WireGuard.UnitLibrary/Tunnel/WireGuardTunnelDnsResolver.cs, Curl.Extension.WireGuard.UnitLibrary/Tunnel/WireGuardConnector.cs, Curl.Extension.WireGuard.UnitTests/Tunnel]
requirement: none
created: 2026-10-31
completed:
---
# BL-4424 — Report a WireGuard tunnel DNS failure as exit 7 on HTTP/3 too, and on every tunnel failure in the DNS exchange

## Goal

Under `--wireguard`, a tunnel that cannot reach its DNS server ends the transfer with exit 7 ("Failed to connect ... through the WireGuard tunnel") whatever the HTTP version, and no tunnel failure during the DNS exchange escapes as an unhandled exception.

## Context

Found by the shift-end review of 2026-10-31 (ADR-0710; type-design-analyzer, Important; silent-failure-hunter too) in the merge `55fa1ce15...ac0e3e86e`. The shift's own task mapped handshake and tunnel DNS failures to exit 7 (ADR-1172).

- `Curl.Extension.WireGuard.UnitLibrary/Tunnel/WireGuardTunnelDnsResolver.cs:23`: `ResolveAsync` returns `resolution.Address is { } address ? [address] : []`, throwing away `TunnelResolution.Failure`. curl's connector resolves an HTTP/3 name through this resolver (the resolver hook, `WireGuardResolverHook.cs:21`) before it dials QUIC, so a tunnel that is down gives "Could not resolve host" and exit 6 over HTTP/3, while the TCP path (`WireGuardConnector.ResolveAndConnectAsync`) and the datagram path (`WireGuardDatagramConnector.NotResolved`) give exit 7 for the same failure.
- `Curl.Extension.WireGuard.UnitLibrary/Tunnel/WireGuardConnector.cs:173`: `ExchangeAsync` catches only `IOException`, but the new `IsTunnelFailure` treats `TimeoutException` and `SocketException` as tunnel failures too. A `SocketException` from the tunnel's UDP socket during the DNS exchange escapes instead of becoming exit 7.

## Acceptance criteria

- [ ] An HTTP/3 transfer under `--wireguard` whose DNS server cannot be reached through the tunnel ends with exit 7 and the same message the TCP path gives; a unit test in `Curl.Extension.WireGuard.UnitTests/Tunnel` pins it (the way the resolver carries the failure to curl's connector - an exception the connector maps, a richer result, or another route - is decided and recorded in ADR-1172).
- [ ] A name the tunnel's DNS server answers with no address still gives exit 6.
- [ ] `ExchangeAsync` turns every exception `IsTunnelFailure` accepts into the tunnel failure; a unit test throws `SocketException` and `TimeoutException` from the exchange and gets exit 7.
- [ ] `dotnet build` is clean and the fast tests pass; the library stays at 100% line and branch coverage.

## Notes

## Log

- 2026-10-31: Created.
