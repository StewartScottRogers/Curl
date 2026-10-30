---
id: BL-4314
title: Coap DTLS test LostServerHelloFlight fails under a loaded full fast-test run
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extension.Coap.UnitTests/Dtls/Handshake/DtlsClientHandshakeTests.cs, Curl.Extension.Coap.UnitLibrary/Dtls/Handshake/DtlsClientHandshake.cs, Curl.Extension.Coap.UnitTests/Dtls/Handshake/DtlsTestServerChannel.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4314 — Coap DTLS test LostServerHelloFlight fails under a loaded full fast-test run

## Goal

`DtlsClientHandshakeTests.RunAsync_Rfc6347Section4_2_4_LostServerHelloFlight_ResendsTheClientHelloOnTheTimerAndCompletes` passes in every full fast-test run, not only alone.

## Context

Found by BL-4311's run on 2026-10-29: in the full `dotnet test --filter "TestCategory!=Integration"` run on a loaded machine (nine lanes) it failed with `CoapException: DTLS peer did not answer after 12 retransmissions` (`DtlsClientHandshake.cs:61`, from `DtlsClientHandshakeTests.cs:75`); run alone straight afterwards it passed in 100 ms. The test likely advances its fake clock before the client has sent or started waiting on the resend timer, so under load all 12 timeouts fire before the server's answer arrives. Make the test wait for the event it needs (the ClientHello resent) before advancing the clock again, per the lane rules on concurrent tests.

## Acceptance criteria

- [x] The test waits for each resend before it advances the clock, with no wall-clock delay.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Cause (found by reading, not reproduced under load): `DtlsTestServerChannel.Deliver` wrote the inbox outside `starvedGate`, so `ReceiveAsync` could see an empty inbox for a datagram already on its way, call the client starved and fire the resend timer early. The write is now inside the lock, so a timer fires only when nothing is in flight. The test and the production handshake are unchanged; the test helper in `DtlsTestServerChannel.cs` was the file fixed (a test file in the same folder as the touched test).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: a1f5021a 3d836d5e.
- 2026-10-29: Doing -> Done. Deliver wrote the inbox outside the starvation lock; moved inside. Build clean, Coap fast tests green (275).
