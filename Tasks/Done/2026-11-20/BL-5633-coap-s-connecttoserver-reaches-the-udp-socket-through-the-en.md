---
id: BL-5633
title: CoAP's ConnectToServer reaches the UDP socket through the engine's recording wrapper and a failed connect keeps a curl exit code
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/EndPointRecordingDatagramConnector.cs, Curl.Engine.UnitTests, Curl.Extended.Protocol.Coap.UnitLibrary/Client/CoapProtocolHandler.cs, Curl.Extended.Protocol.Coap.UnitTests/Client/CoapProtocolHandlerTests.cs]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-5633 — CoAP's ConnectToServer reaches the UDP socket through the engine's recording wrapper and a failed connect keeps a curl exit code

## Goal

`curl coap://127.0.0.1:<unbound port>/` fails at once with exit 7 in the real executable, as ADR-1318's connected UDP socket intends, and a UDP connect that the system refuses still ends with a curl exit code.

## Context

Found by the shift-end review of 2026-11-20 in the diff b73256235592..071c3980c0:
- type-design-analyzer found the missing forward.
- silent-failure-hunter found the raw exception.

1. **The connect is dropped by the recording wrapper.** `IDatagramChannel.ConnectToServer()` (`Curl.Protocol.Abstractions.UnitLibrary/IDatagramChannel.cs:36-43`) was added as a default interface method that does nothing. `CoapProtocolHandler.cs:68` calls it.
   - In production the extension's `DatagramConnector` is the engine's `EndPointRecordingDatagramConnector`.
   - That connector wraps the transfer's first channel in `ReRecordingChannel` (`Curl.Engine.UnitLibrary/EndPointRecordingDatagramConnector.cs` about line 47), which does not forward `ConnectToServer`.
   - So the call hits the no-op default, the socket is never connected, and an unbound port waits out CoAP's retransmissions instead of failing at once.
   - The unit tests skip the recorder, so they pass.
2. **A refused connect escapes as a raw exception.** `transport.ConnectToServer()` ends in `UdpDatagramChannel.ConnectToServer`, then `Socket.Connect`, which can throw `SocketException` (ENETUNREACH or EHOSTUNREACH on Linux and macOS). The call sits outside every handler that maps socket errors to an exit code. Before the change, that failure surfaced on the first send as exit 55, "CoAP send failed".

## Acceptance criteria

- [x] `ReRecordingChannel` forwards `ConnectToServer` to its inner channel, and a test in `Curl.Engine.UnitTests` proves it.
- [x] Any other `IDatagramChannel` wrapper the CoAP path can be handed forwards it too, or the default is removed so each wrapper must implement it.
- [x] A `SocketException` from `ConnectToServer` in `CoapProtocolHandler` ends the transfer with the exit code the send path uses (55, "CoAP send failed: ..."), and a `CoapProtocolHandlerTests` test pins it.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Other IDatagramChannel wrappers (Masque, QuicMigration, WireGuard, Capsule, DTLS) are tunnels or sit above the socket; none wraps the socket channel the CoAP path connects, so the no-op default stays (removing it would touch ~10 files outside touches). Only ReRecordingChannel wrapped the socket channel (PortUnreachableReportingDatagramChannel already forwards).

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing. Criteria at claim: 29999200 f82a7011 2aa4168e 3d836d5e.
- 2026-11-20: Doing -> Done. ReRecordingChannel forwards ConnectToServer; a SocketException from it ends CoAP with exit 55; tests added, build and fast tests green, quality gate clean.
