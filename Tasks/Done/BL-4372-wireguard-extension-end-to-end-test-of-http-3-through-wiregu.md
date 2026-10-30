---
id: BL-4372
title: WireGuard extension: end-to-end test of HTTP/3 through --wireguard with a fake peer
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4147, BL-4354, BL-4357, BL-4371]
touches: [Curl.Extension.WireGuard.UnitTests/Tunnel, Curl.Console/CurlComposition.cs, Curl.Extension.WireGuard.UnitTests/Curl.Extension.WireGuard.UnitTests.csproj, Curl.Console/Curl.Console.csproj]
requirement: none
created: 2026-10-30
completed: 2026-10-30
---
# BL-4372 — WireGuard extension: end-to-end test of HTTP/3 through --wireguard with a fake peer

## Goal

A unit test runs `curl --wireguard wg0.conf --http3-only https://10.0.0.1/` end to end, with the QUIC datagrams travelling through the tunnel to a fake peer.

## Context

BL-4371 carries UDP inside the tunnel and BL-4357 made the in-memory dialing runner build its QUIC dialer over the hook's datagram connector. This test proves them together. Split from BL-4355 (second half).

## Acceptance criteria

- [x] An HTTP/3 transfer's QUIC datagrams travel through the tunnel in a unit test with a fake peer.
- [x] The four quality gates hold by Measure-CodeQuality.ps1.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Split from BL-4355, which went over its cost cap.
- Touches widened to Curl.Console/CurlComposition.cs (no task in Doing on origin/work/dark-factory names it): the dialing runner had no connection-hook activator seam (it always used ExtensionConnectionHookCatalogue.Entries, which reads wg0.conf from disk and opens a real UDP socket), and its QUIC dialer opened through curl's datagram connector, not the hook's, so QUIC under `--wireguard` left outside the tunnel there. Added `extensionConnectionHookActivators` to the dialing `CreateRunner` (defaulted by `ConnectionHookActivatorsOf`, keeping `CreateRunner` within the complexity gate) and built the QUIC dialer over `connectionHooks.DatagramConnectorOver(datagramConnector)`, which gives curl's connector back with no hook. No new ADR: this applies ADR-1163 to the dialing runner.
- Test: `WireGuardHttp3EndToEndTests`; the peer is `TunnelTestPeer` (its `NextSentAsync(CancellationToken)` already existed). The test's resolver answers address literals, since `TcpConnector` resolves the literal 10.0.0.1 through it; curl's datagram connector throws, so the test fails if QUIC leaves the tunnel. Server certificate: self-signed RSA through `Http3ServerTlsSettings.FromRsaCertificate`, as the Http3Server tests use.
- Gates: `Measure-CodeQuality.ps1 -Library Curl.Console` reports 0 failing members (first run found `CreateRunner` at complexity 12 and a helper at 50% branch; both fixed). Linux and macOS: the test is in-memory, with no paths, OS errors or platform certificates, and waits on the datagrams it relays, so it is platform-neutral; fast tests green here.
- Plan: in `Curl.Extension.WireGuard.UnitTests` (its csproj references `Curl.Console` and `Curl.Conformance.Http3Server.UnitLibrary`; `Curl.Console.csproj` has `InternalsVisibleTo` for it, add them if BL-4371 did not), run `CurlComposition.CreateRunner(stdout, stderr, in, ITcpDialer, IDnsResolver, IDatagramConnector, parsesAsQuicBuild: true, ...)` with `["--wireguard","wg0.conf","--http3-only","-k","https://10.0.0.1/"]`, the connection hook activator `values => WireGuardExtension.Activate(values, _ => TunnelTestPeer.ConfigurationText(), peer, TimeProvider.System, RandomNumberGenerator.Create())` (check the dialing overload accepts hook activators; else use the catalogue), curl's datagram connector one that fails the test, and a relay: `TunnelTestPeer` (new `NextSentAsync(CancellationToken)`) opens each transport message, reads the UDP datagram and sends its payload to `UpstreamHttp3ServerFactory.Create(backend, new Http3ServerTlsSettings { CertificateChain = [cert.RawData], SigningKey = new EcdsaTlsSigningKey(key), QuicTransportParameters = Http3ServerConnection.DefaultTransportParameters.Encode() })`'s channel, sealing each reply back as UDP from 10.0.0.1:443; backend an `IConnector` answering `HTTP/1.1 200 OK`.

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: 6291ef19 62dbae4c 3d836d5e.
- 2026-10-30: Doing -> Done. curl --wireguard --http3-only runs end to end in a unit test, its QUIC datagrams sealed through the tunnel to a fake peer
