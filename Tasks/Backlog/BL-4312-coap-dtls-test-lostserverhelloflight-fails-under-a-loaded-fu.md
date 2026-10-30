---
id: BL-4312
title: Coap DTLS test LostServerHelloFlight fails under a loaded full fast-test run
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extension.Coap.UnitTests/Dtls/Handshake/DtlsClientHandshakeTests.cs, Curl.Extension.Coap.UnitLibrary/Dtls/Handshake/DtlsClientHandshake.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4312 — Coap DTLS test LostServerHelloFlight fails under a loaded full fast-test run

## Goal

`DtlsClientHandshakeTests.RunAsync_Rfc6347Section4_2_4_LostServerHelloFlight_ResendsTheClientHelloOnTheTimerAndCompletes` passes in every full fast-test run, not only alone.

## Context

Found by BL-4311's run on 2026-10-29: in the full `dotnet test --filter "TestCategory!=Integration"` run on a loaded machine (nine lanes) it failed with `CoapException: DTLS peer did not answer after 12 retransmissions` (`DtlsClientHandshake.cs:61`, from `DtlsClientHandshakeTests.cs:75`); run alone straight afterwards it passed in 100 ms. The test likely advances its fake clock before the client has sent or started waiting on the resend timer, so under load all 12 timeouts fire before the server's answer arrives. Make the test wait for the event it needs (the ClientHello resent) before advancing the clock again, per the lane rules on concurrent tests.

## Acceptance criteria

- [ ] The test waits for each resend before it advances the clock, with no wall-clock delay.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
