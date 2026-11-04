---
id: BL-4786
title: Let a closed UDP port escape the CoAP and DNS UDP socket-error catches as exit 7
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extended.Protocol.Coap.UnitLibrary/Client/CoapExchange.cs, Curl.Extended.Protocol.Coap.UnitTests/Client, Curl.Extended.Protocol.DnsQuery.UnitLibrary/Transport/Udp/DnsUdpTransport.cs, Curl.Extended.Protocol.DnsQuery.UnitTests/Transport/DnsUdpTransportTests.cs]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4786 — Let a closed UDP port escape the CoAP and DNS UDP socket-error catches as exit 7

## Goal

`coap://` and `dns://` (UDP) to a closed UDP port exit 7 with the connector's "Failed to connect to ..." text again, as ADR-1224 decided, while other socket errors keep exit 55 (send) and 56 (receive).

## Context

Found by the shift-end review of 2026-11-03 (silent-failure-hunter and code-reviewer, both Critical), in the shift merged at 7410f102c.

- The shift added `catch (SocketException)` blocks in `Curl.Extended.Protocol.Coap.UnitLibrary/Client/CoapExchange.cs` (`ReceivedAsync` and `SendAsync`, ~lines 181-204) and in `Curl.Extended.Protocol.DnsQuery.UnitLibrary/Transport/Udp/DnsUdpTransport.cs` (`SendQueryAsync` ~line 92, `ReceiveAnswerAsync` ~line 119). They rethrow as `CoapException`/`DnsQueryException` with exit 55 or 56.
- `Curl.Networking.DatagramPortUnreachableException` derives from `SocketException` (`Curl.Networking.UnitLibrary/DatagramPortUnreachableException.cs:15`). `PortUnreachableReportingDatagramChannel` throws it for a refusal before the first datagram arrives, and `Curl.Engine.UnitLibrary/EndPointReportingProtocolHandler.cs:41` turns it into exit 7 (BL-4654, BL-4675, ADR-1224, which names `coap` and `dns`).
- The new catches run first, so the exit-7 path is never reached: a closed port now gives 55 or 56.
- The new tests (`CoapExchangeSocketErrorTests`, `DnsUdpTransportTests`) throw only a plain `SocketException`, so they miss it.
- DnsQuery already references `Curl.Networking.UnitLibrary`. Coap references only Abstractions and Tls, so it needs a filter that does not reference `Curl.Networking` (or the type moves to `Curl.Protocol.Abstractions`). Do not break the extension isolation rule.

## Acceptance criteria

- [x] A `DatagramPortUnreachableException` thrown by the channel's send or receive escapes `CoapExchange` unchanged. A unit test in `Curl.Extended.Protocol.Coap.UnitTests` asserts it for both send and receive.
- [x] The same holds for `DnsUdpTransport`, with a unit test for both send and receive in `Curl.Extended.Protocol.DnsQuery.UnitTests`.
- [x] A plain `SocketException` still ends with exit 55 on send and exit 56 on receive; the existing tests stay green.
- [x] `VerifyExtensionIsolation` still passes, and both libraries keep 100% line and branch coverage.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Decision: the catches filter `when (exception.GetType() == typeof(SocketException))`, so any subclass (DatagramPortUnreachableException) escapes to the engine's exit 7 mapping with no Curl.Networking reference in Coap. Tests use a local SocketException subclass. Quality run: 0 failing members.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 9099dbe7 0f270ae7 bb51a515 4cca8abe 3d836d5e.
- 2026-11-03: Doing -> Done. CoAP and DNS UDP catches now only catch a plain SocketException; DatagramPortUnreachableException escapes for exit 7
