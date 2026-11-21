---
id: BL-5637
title: Fall back to the next address when a CoAP UDP connect fails, and connect before reporting the connection opened
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extended.Protocol.Coap.UnitLibrary/Client/CoapProtocolHandler.cs, Curl.Networking.UnitLibrary/PortUnreachableReportingDatagramChannel.cs, Curl.Extended.Protocol.Coap.UnitTests, Curl.Networking.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-5637 — Fall back to the next address when a CoAP UDP connect fails, and connect before reporting the connection opened

## Goal

A CoAP transfer whose first resolved address refuses the UDP connect tries the next address, as
the send and receive paths already do, and `-v` writes `Established connection` only once the
connect has succeeded.

## Context

Found by the dark factory's shift-end review of 2026-11-20 (ADR-0710; pr-review-toolkit
silent-failure-hunter, verified against the code) in the shift that merged BL-5633
(commit bc3205761a).

- BL-5633 made `EndPointRecordingDatagramConnector`'s channel forward `ConnectToServer`, so the
  connect now really reaches the socket, and `CoapProtocolHandler.ConnectFailure`
  (`Curl.Extended.Protocol.Coap.UnitLibrary/Client/CoapProtocolHandler.cs`) maps a
  `SocketException` from it to exit 55 at once.
- `Curl.Networking.UnitLibrary/PortUnreachableReportingDatagramChannel.cs`, `ConnectToServer`, is a
  bare pass-through: unlike `SendAsync` and `ReceiveAsync` it never calls
  `SwitchToNextAddressAsync`, though `OpenAndResendAsync` already connects the next channel when
  `_connectsToServer` is set. So `coap://localhost/x` on a host where the UDP `connect()` to `::1`
  fails (IPv6 off: `ENETUNREACH`/`EADDRNOTAVAIL`) ends with exit 55 without trying `127.0.0.1`.
- `CoapProtocolHandler` calls `ReportConnectionOpened` before the connect, so `-v` prints
  `Established connection` just before the failure; the failure text says `CoAP send failed` for a
  connect.

## Acceptance criteria

- [x] A connect that throws `SocketException` on the first address with alternatives left opens and connects the next address, pinned by a `Curl.Networking.UnitTests` test with fake channels.
- [x] A connect that fails on every address still ends a CoAP transfer with exit 55 (the existing `CoapProtocolHandlerTests` case stays green).
- [x] `CoapProtocolHandler` reports the connection opened only after the connect succeeds, and a failed connect's message says connect, not send; pinned by a `CoapProtocolHandlerTests` test.
- [x] `dotnet build` is clean and the fast tests pass; both libraries keep 100% line and branch coverage.

## Notes

- ConnectToServer now falls back to the next resolved address on any SocketException (not only refusals, since the connect failure is ENETUNREACH or EADDRNOTAVAIL); failed channels are disposed with the channel. Handler connects first, reports opened after, message says "CoAP connect failed". Decided by default, no ADR needed. CI on Linux and macOS: tests are platform-neutral.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing. Criteria at claim: a524b3a0 acc6b9ed 02d69108 069dd186.
- 2026-11-20: Doing -> Done. Connect falls back to the next address; connection reported opened after the connect
