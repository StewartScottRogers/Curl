---
id: BL-4372
title: WireGuard extension: end-to-end test of HTTP/3 through --wireguard with a fake peer
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4147, BL-4354, BL-4357, BL-4371]
touches: [Curl.Extension.WireGuard.UnitTests/Tunnel, Curl.Extension.WireGuard.UnitTests/Curl.Extension.WireGuard.UnitTests.csproj, Curl.Console/Curl.Console.csproj]
requirement: none
created: 2026-10-30
completed:
---
# BL-4372 — WireGuard extension: end-to-end test of HTTP/3 through --wireguard with a fake peer

## Goal

A unit test runs `curl --wireguard wg0.conf --http3-only https://10.0.0.1/` end to end, with the QUIC datagrams travelling through the tunnel to a fake peer.

## Context

BL-4371 carries UDP inside the tunnel and BL-4357 made the in-memory dialing runner build its QUIC dialer over the hook's datagram connector. This test proves them together. Split from BL-4355 (second half).

## Acceptance criteria

- [ ] An HTTP/3 transfer's QUIC datagrams travel through the tunnel in a unit test with a fake peer.
- [ ] The four quality gates hold by Measure-CodeQuality.ps1.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- Split from BL-4355, which went over its cost cap.
- Plan: in `Curl.Extension.WireGuard.UnitTests` (its csproj references `Curl.Console` and `Curl.Conformance.Http3Server.UnitLibrary`; `Curl.Console.csproj` has `InternalsVisibleTo` for it, add them if BL-4371 did not), run `CurlComposition.CreateRunner(stdout, stderr, in, ITcpDialer, IDnsResolver, IDatagramConnector, parsesAsQuicBuild: true, ...)` with `["--wireguard","wg0.conf","--http3-only","-k","https://10.0.0.1/"]`, the connection hook activator `values => WireGuardExtension.Activate(values, _ => TunnelTestPeer.ConfigurationText(), peer, TimeProvider.System, RandomNumberGenerator.Create())` (check the dialing overload accepts hook activators; else use the catalogue), curl's datagram connector one that fails the test, and a relay: `TunnelTestPeer` (new `NextSentAsync(CancellationToken)`) opens each transport message, reads the UDP datagram and sends its payload to `UpstreamHttp3ServerFactory.Create(backend, new Http3ServerTlsSettings { CertificateChain = [cert.RawData], SigningKey = new EcdsaTlsSigningKey(key), QuicTransportParameters = Http3ServerConnection.DefaultTransportParameters.Encode() })`'s channel, sealing each reply back as UDP from 10.0.0.1:443; backend an `IConnector` answering `HTTP/1.1 200 OK`.

## Log

- 2026-10-30: Created.
