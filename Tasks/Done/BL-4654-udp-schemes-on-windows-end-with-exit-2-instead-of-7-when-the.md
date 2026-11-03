---
id: BL-4654
title: UDP schemes on Windows end with exit 2 instead of 7 when the port is closed
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/UdpChannelOpener.cs, Curl.Networking.UnitLibrary/UdpDatagramConnector.cs, Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs, Curl.Networking.UnitTests/UdpChannelOpenerTests.cs, Curl.Networking.UnitLibrary/PortUnreachableReportingDatagramChannel.cs, Curl.Networking.UnitLibrary/DatagramPortUnreachableException.cs, Curl.Networking.UnitTests/PortUnreachableReportingDatagramChannelTests.cs, Curl.Networking.UnitTests/UdpDatagramConnectorTests.cs, Curl.Engine.UnitLibrary/EndPointReportingProtocolHandler.cs, Curl.Engine.UnitTests/EndPointReportingProtocolHandlerTests.cs]
requirement: none
created: 2026-11-02
completed: 2026-11-03
---
# BL-4654 — UDP schemes on Windows end with exit 2 instead of 7 when the port is closed

## Goal

A `coap`, `coaps`, `dns` or `webtransport` transfer to a closed UDP port on Windows ends with exit 7 (manual, EXIT CODES 7), not exit 2.

## Context

Found by BL-4612 running Curl.Console's curl.exe: `curl -sS coap://localhost:1/x` (and dns, webtransport) ends with `curl: (2) An existing connection was forcibly closed by the remote host.` - Windows reports the ICMP port-unreachable as SocketError.ConnectionReset on the UDP socket, which escapes as exit 2. Measure what curl does for a closed UDP port (HTTP/3 and TFTP) with Record-CurlExchange.ps1 before pinning the text; the in-process conformance harness cannot show this (no real socket).

## Acceptance criteria

- [x] A closed UDP port maps to exit 7 with curl's text, pinned by a unit test with a fake datagram socket that reports ConnectionReset.
- [x] Build clean and fast tests green; coverage gates hold.

## Notes

- Measured (Record-CurlExchange.ps1, Windows Schannel curl): `tftp://127.0.0.1:1/x` is `curl: (7) Received too short packet`; this curl has no HTTP/3. Our TFTP already matched; coap, dns and webtransport gave exit 2.
- Fix (ADR-1224): `UdpDatagramConnector` wraps each channel in `PortUnreachableReportingDatagramChannel`, which turns ConnectionReset/ConnectionRefused into `DatagramPortUnreachableException` (a SocketException, so TFTP's catch is unchanged) carrying the connector's exit 7 text; the engine's `EndPointReportingProtocolHandler` maps it to exit 7. Measured after: coap, dns and webtransport to port 1 print `curl: (7) Failed to connect to <host>:1 after N ms: Could not connect to server`.
- Touches widened to the two new Networking files and their tests, `UdpDatagramConnectorTests.cs` (one test pinned the channel type) and the engine's `EndPointReportingProtocolHandler.cs` and its tests, where the escape is caught; no task in Doing held them. `UdpChannelOpener.cs` and `TcpConnector.UdpTunnel.cs` needed no change; the unit tests are in `PortUnreachableReportingDatagramChannelTests.cs` rather than `UdpChannelOpenerTests.cs`.
- Quality: Measure-CodeQuality.ps1 for Networking and Engine found one branch gap in the new `FailedToConnectMessage` (an `OpenFailure` copy of its text was left behind); fixed by making `OpenFailure` call it, which the existing mapped-destination test covers. Every test project the run reached passed, and Networking.UnitTests passed again after the fix. The tests use fake channels only, so they don't depend on the platform; that covers the CI criterion for Linux and macOS.

## Log

- 2026-11-02: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 01bb1e05 8a80dee3.
- 2026-11-03: Doing -> Done. A coap, dns or webtransport transfer to a closed UDP port ends with exit 7 and the connector's Failed to connect text
