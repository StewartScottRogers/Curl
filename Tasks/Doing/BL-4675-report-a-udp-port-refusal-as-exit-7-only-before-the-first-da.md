---
id: BL-4675
title: Report a UDP port refusal as exit 7 only before the first datagram is received
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/PortUnreachableReportingDatagramChannel.cs, Curl.Networking.UnitLibrary/UdpDatagramConnector.cs, Curl.Networking.UnitTests/PortUnreachableReportingDatagramChannelTests.cs, Curl.Networking.UnitTests/UdpDatagramConnectorTests.cs]
requirement: none
created: 2026-11-03
completed:
---
# BL-4675 — Report a UDP port refusal as exit 7 only before the first datagram is received

## Goal

A UDP transfer whose server port is refused after the server has already answered ends with the handler's own receive or send failure, not with exit 7 `Failed to connect`.

## Context

Found by the shift-end review (ADR-0710) of `f37ba71c...dfa29449`: type-design-analyzer.

BL-4654 / ADR-1224 added `Curl.Networking.UnitLibrary/PortUnreachableReportingDatagramChannel.cs`, which `UdpDatagramConnector.cs:149` wraps around every channel it opens. The wrapper turns every `ConnectionReset` or `ConnectionRefused` on any send or receive, for the channel's whole life, into a `DatagramPortUnreachableException`, and `Curl.Engine.UnitLibrary/EndPointReportingProtocolHandler.cs` (~line 41) makes that `TransferResult.Failure(CouldntConnect, ...)`. The message is built when the refusal is seen, timed from `openStarted`, so it names the whole transfer time; the failure also carries no byte count, so `%{size_download}` loses bytes already written.

Scenario: a CoAP blockwise transfer refused after the first blocks arrived prints `Failed to connect ... after <whole transfer time> ms` and exits 7. ADR-1224 is about a port that is closed from the start, before any answer.

Fix: translate the refusal only until the channel has received its first datagram (a flag set in `ReceiveAsync`); after that, let the `SocketException` through unchanged.

## Acceptance criteria

- [ ] `PortUnreachableReportingDatagramChannelTests` has a test where a receive succeeds and a later receive fails with `ConnectionRefused`, and the plain `SocketException` (not `DatagramPortUnreachableException`) escapes.
- [ ] The existing test for a refusal before any datagram still gives `DatagramPortUnreachableException` with curl's exit 7 text.
- [ ] The wrapper's doc comment and `UdpDatagramConnector.FailedToConnectMessage`'s say which refusals they cover.
- [ ] `dotnet build` is clean and the fast tests of `Curl.Networking.UnitTests` and `Curl.Engine.UnitTests` pass.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 57062642 abbe5c45 2b57dc98 13a4f46b.
