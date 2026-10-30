---
id: BL-4355
title: WireGuard extension: carry UDP inside the tunnel so HTTP/3 runs through --wireguard
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4147, BL-4354, BL-4357]
touches: [Curl.Extension.WireGuard.UnitLibrary/IpStack/Udp, Curl.Extension.WireGuard.UnitTests/IpStack/Udp, Curl.Extension.WireGuard.UnitLibrary/Tunnel/WireGuardDatagramConnector.cs, Curl.Extension.WireGuard.UnitLibrary/Tunnel/WireGuardTunnelLifetime.cs, Curl.Extension.WireGuard.UnitLibrary/Tunnel/WireGuardConnector.cs, Curl.Extension.WireGuard.UnitLibrary/WireGuardConnectionHook.cs, Curl.Extension.WireGuard.UnitLibrary/CLAUDE.md, Curl.Extension.WireGuard.UnitTests/Tunnel, Curl.Extension.WireGuard.UnitTests/WireGuardConnectionHookTests.cs, Curl.Extension.WireGuard.UnitTests/Curl.Extension.WireGuard.UnitTests.csproj, Curl.Console/Curl.Console.csproj]
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

- 2026-10-30 run (lane 7): criterion 1 done in the working tree (stashed by the shift): `IpStack/Udp/UdpDatagramCodec` (RFC 768, checksum over the pseudo-header, zero sent as all ones, zero accepted over IPv4 and refused over IPv6), `UdpDatagram`, `UserSpaceUdpChannel` (an `IDatagramChannel` over `IIpPacketChannel`); `WireGuardDatagramConnector` opens each channel in its own tunnel (`WireGuardTunnelLifetime` closes socket and keys), host names resolved by `WireGuardConnector.ResolveThroughTunnelAsync`; the hook gives it. Recorded datagrams: "hi" 10.0.0.2:50000 -> 10.0.0.1:443 checksum 0xBE62, [fd00::2] -> [fd00::1] 0xD860 (summed by hand and checked with an independent C# sum). WireGuard tests 308 green; build clean. ADR-1162 written (uncommitted, with the code).
- HTTP/3 wiring found: the HTTP handler calls `ConnectMultiplexedAsync` on the hook's `WireGuardConnector`, which refused QUIC. Decided (ADR-1162): it now hands QUIC to curl's connector (the hook passes it), whose QUIC dialer opens UDP through the hook's datagram connector (BL-4318) and so through the tunnel; unit tests in `WireGuardConnectionHookTests`.
- Criterion 2 is left: the end-to-end test. Plan: in `Curl.Extension.WireGuard.UnitTests` (its csproj now references `Curl.Console` and `Curl.Conformance.Http3Server.UnitLibrary`; `Curl.Console.csproj` has `InternalsVisibleTo` for it), run `CurlComposition.CreateRunner(stdout, stderr, in, ITcpDialer, IDnsResolver, IDatagramConnector, parsesAsQuicBuild: true, ...)` with `["--wireguard","wg0.conf","--http3-only","-k","https://10.0.0.1/"]`, the connection hook activator `values => WireGuardExtension.Activate(values, _ => TunnelTestPeer.ConfigurationText(), peer, TimeProvider.System, RandomNumberGenerator.Create())` (check the dialing overload accepts hook activators; else use the catalogue), curl's datagram connector one that fails the test, and a relay: `TunnelTestPeer` (new `NextSentAsync(CancellationToken)`) opens each transport message, reads the UDP datagram and sends its payload to `UpstreamHttp3ServerFactory.Create(backend, new Http3ServerTlsSettings { CertificateChain = [cert.RawData], SigningKey = new EcdsaTlsSigningKey(key), QuicTransportParameters = Http3ServerConnection.DefaultTransportParameters.Encode() })`'s channel, sealing each reply back as UDP from 10.0.0.1:443; backend an `IConnector` answering `HTTP/1.1 200 OK`. That runner builds its QUIC dialer over `EngineDialing.DatagramConnector` without the hook, so BL-4357 must land first.
- Then: update `Curl.Extension.WireGuard.UnitLibrary/CLAUDE.md` (UDP and QUIC, no longer "TCP only"), run Measure-CodeQuality.ps1 -Library Curl.Extension.WireGuard.UnitLibrary, the fast tests, and commit. Touches widened for the files above (no task in Doing on the shared branch held them).

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: 954a2d25 6291ef19 62dbae4c 3d836d5e.
- 2026-10-30: Doing -> Backlog. Waits on BL-4357 (Engine: the in-memory dialing runner's QUIC dialer must use the hook's datagram connector) for its HTTP/3 end-to-end test; UDP through the tunnel is done in the stashed working tree
- 2026-10-30: Backlog -> Deferred. Over its cost cap; split into BL-4371 (UDP in the tunnel) and BL-4372 (HTTP/3 end-to-end test)
