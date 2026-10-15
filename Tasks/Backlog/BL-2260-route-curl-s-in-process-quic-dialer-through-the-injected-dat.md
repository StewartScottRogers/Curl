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
completed:
---
# BL-2260 — Route Curl's in-process QUIC dialer through the injected datagram connector

## Goal

An in-process run of Curl (`InProcessCurl.RunAsync` with an `IDatagramConnector`, as the upstream case runner calls it) sends its `--http3` / `--http3-only` QUIC datagrams through that injected connector, not through a real UDP socket, so the HTTP/3 stand-in (`Curl.Conformance.Http3Server.UnitLibrary`, BL-2231) can answer them in memory.

## Context

Found by BL-2231's first pass (2026-10-14): `CurlComposition` builds `new QuicDialer(tlsClientOptions, timeProvider)` (around line 758), whose default constructor opens sockets through `UdpChannelOpener`; the injected `IDatagramConnector` reaches only TFTP. `QuicDialer` already takes an `IUdpChannelOpener` in its internal constructor, so an `IUdpChannelOpener` adapter over `IDatagramConnector` (opening the channel to the endpoint's address and port) is likely enough, wired in where the run was given a datagram connector. Production runs must keep the real UDP opener and its `--interface` / `--local-port` binding behaviour.

## Acceptance criteria

- [ ] An in-process run given an `IDatagramConnector` opens its QUIC channel through it; a unit test with a fake connector pins the open (host, port) and that the datagrams go through it.
- [ ] A production run still opens real UDP sockets; existing QUIC/HTTP/3 tests stay green.
- [ ] 100% line and branch coverage and the complexity gate hold for every changed library; `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-10-14: Created.
