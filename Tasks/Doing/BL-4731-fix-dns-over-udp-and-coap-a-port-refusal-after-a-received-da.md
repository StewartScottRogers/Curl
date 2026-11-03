---
id: BL-4731
title: Fix dns:// over UDP and CoAP: a port refusal after a received datagram escapes as exit 2
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extended.Protocol.DnsQuery.UnitLibrary/Transport/Udp/DnsUdpTransport.cs, Curl.Extended.Protocol.DnsQuery.UnitTests/Transport/DnsUdpTransportTests.cs, Curl.Extended.Protocol.Coap.UnitLibrary/Client/CoapExchange.cs, Curl.Extended.Protocol.Coap.UnitTests/Client]
requirement: none
created: 2026-11-03
completed:
---
# BL-4731 — Fix dns:// over UDP and CoAP: a port refusal after a received datagram escapes as exit 2

## Goal

When a `dns://` UDP query or a CoAP exchange has already received a datagram and the server's port then refuses (ICMP port unreachable), the transfer ends with a curl receive or send error, not `curl: (2)` with a raw .NET socket message.

## Context

Found by the shift-end review of 2026-11-03 (silent-failure-hunter; diff 58b155fc2...04010e380, BL-4675).

- `Curl.Networking.UnitLibrary/PortUnreachableReportingDatagramChannel.cs:47,63-67` now converts `ConnectionReset`/`ConnectionRefused` into `DatagramPortUnreachableException` (exit 7) only until the first datagram arrives. After that, the raw `SocketException` passes through "for the handler to report as its own receive or send failure".
- Any datagram sets `_hasReceived`, including one the handler discards. `DnsUdpTransport` discards datagrams with a wrong ID or (new in this diff, ADR-1225) a wrong question.
- `Curl.Extended.Protocol.DnsQuery.UnitLibrary/Transport/Udp/DnsUdpTransport.cs:85-105` (`ReceiveAnswerAsync`) catches only `OperationCanceledException`, so the `SocketException` escapes `DnsQueryProtocolHandler` and `UnhandledExceptionGuard` reports exit 2.
- CoAP's receive loop (`Curl.Extended.Protocol.Coap.UnitLibrary/Client/CoapExchange.cs`) has no `SocketException` handler either. The TFTP and KDC transports already handle this case.

## Acceptance criteria

- [ ] `DnsUdpTransport` turns a `SocketException` on receive into a `DnsQueryException` with exit 56 (`RecvError`), and on send into exit 55 (`SendError`); unit tests use a stray datagram followed by a refusal.
- [ ] CoAP's exchange reports the same case with its own receive or send exit code instead of letting the `SocketException` escape (unit test).
- [ ] Exit 7 is still reported for a refusal before any datagram arrives (existing tests stay green).
- [ ] `dotnet build` is clean, the fast tests pass, and both libraries keep 100% line and branch coverage.
## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 9c08d223 0c7447a0 f1b89992 89ae1a50.
