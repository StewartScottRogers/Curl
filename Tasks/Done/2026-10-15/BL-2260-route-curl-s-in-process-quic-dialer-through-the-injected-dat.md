---
id: BL-2260
title: Route Curl's in-process QUIC dialer through the injected datagram connector
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
model: opus
requirement: none
created: 2026-10-14
completed: 2026-10-14
---
# BL-2260 — Route Curl's in-process QUIC dialer through the injected datagram connector

## Goal

An in-process run of Curl (`InProcessCurl.RunAsync` with an `IDatagramConnector`, as the upstream case runner calls it) sends its `--http3` / `--http3-only` QUIC datagrams through that injected connector, not through a real UDP socket, so the HTTP/3 stand-in (`Curl.Conformance.Http3Server.UnitLibrary`, BL-2231) can answer them in memory.

## Context

Found by BL-2231's first pass (2026-10-14): `CurlComposition` builds `new QuicDialer(tlsClientOptions, timeProvider)` (around line 758), whose default constructor opens sockets through `UdpChannelOpener`; the injected `IDatagramConnector` reaches only TFTP. `QuicDialer` already takes an `IUdpChannelOpener` in its internal constructor, so an `IUdpChannelOpener` adapter over `IDatagramConnector` (opening the channel to the endpoint's address and port) is likely enough, wired in where the run was given a datagram connector. Production runs must keep the real UDP opener and its `--interface` / `--local-port` binding behaviour.

## Acceptance criteria

- [x] An in-process run given an `IDatagramConnector` opens its QUIC channel through it; a unit test with a fake connector pins the open (host, port) and that the datagrams go through it.
- [x] A production run still opens real UDP sockets; existing QUIC/HTTP/3 tests stay green.
- [x] 100% line and branch coverage and the complexity gate hold for every changed library; `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Plan: `DatagramConnectorUdpChannelOpener` (Curl.Networking) adapts `IDatagramConnector` to `IUdpChannelOpener`. `IUdpChannelOpener.Open` is synchronous and the connector is async, so the channel opens lazily on its first send or receive (both await the one open, so a receive started before the first send is safe) rather than blocking on `.Result`. `OpenFrom` and `OpenFromDeviceAsync` open the same way and bind nothing: the connector picks its own local end, as `DatagramConnectorDnsSocketOpener` does (BL-2248). `QuicDialer` gained a public `(IUdpChannelOpener, TlsClientOptions, TimeProvider)` constructor.
- Wiring: the Context named `CreateTransports` (line 758), but that is the executable's path. The in-process dialing runner (`CreateDialingTransferDispatch`) built its `TcpConnector` with no `QuicDialer` at all (`--http3` ended exit 7, `QUIC is not available on this connector`); it now gets `CurlComposition.CreateDatagramConnectorQuicDialer`. `CreateTransports` is unchanged, so the executable keeps real UDP sockets and its `--interface` / `--local-port` binding.
- Choice: the dialing `CreateRunner` gained `parsesAsWindowsBuild` (default `null`, this platform's build), as the `IConnector` overload has (ADR-0397), because on Windows the Schannel build's parser refuses `--http3` with exit 2. For BL-2261: `InProcessCurl.RunAsync` passes none, so on Windows upstream HTTP/3 cases still parse as the Schannel build and exit 2 unless the harness reads them as the OpenSSL build.
- Tests: `DatagramConnectorUdpChannelOpenerTests` (8) and `CurlCommandRunnerHttp3DatagramConnectorTests` (2): `--http3-only https://127.0.0.1:47260/` opens `127.0.0.1:47260` through the fake connector and sends a 1200-byte-or-more Initial through it, ending exit 56 on the fake's reset receive. `Measure-CodeQuality.ps1 -Library Curl.Console,Curl.Networking.UnitLibrary`: 0 failing members. Fast tests green.

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Done. In-process runs send --http3 QUIC datagrams through the injected IDatagramConnector (DatagramConnectorUdpChannelOpener); the executable keeps real UDP sockets
