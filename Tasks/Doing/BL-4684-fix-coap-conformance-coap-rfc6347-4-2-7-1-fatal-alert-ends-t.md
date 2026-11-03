---
id: BL-4684
title: Fix Coap conformance: Coap:RFC6347-4.2.7-1 fatal alert ends the DTLS handshake
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Coap.UnitLibrary/Dtls/Handshake/DtlsClientHandshake.cs, Curl.Extended.Protocol.Coap.UnitTests/Dtls/Handshake, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-03
completed:
---
# BL-4684 — Fix Coap conformance: Coap:RFC6347-4.2.7-1 fatal alert ends the DTLS handshake

## Goal

Curl's `coaps://` client ends the DTLS handshake at once with exit 35 when the server sends a fatal alert, and sends nothing more.

## Context

Found by BL-4574. RFC 6347 section 4.2.7 and RFC 5246 section 7.2: on a fatal alert the connection ends. `DtlsClientHandshake.ReadServerFlightAsync` keeps only Handshake records, so it ignores the alert, retransmits its ClientHello until the timer gives up and only then exits 35. Case: `CoapDtlsAlertTests` (sends 2 records expected, more seen).

## Acceptance criteria

- [ ] `CoapDtlsAlertTests.FatalAlertInPlaceOfTheHelloFlight_CurlStopsWithExit35AndSendsNothingMore` passes.
- [ ] A focused unit test in `Curl.Extended.Protocol.Coap.UnitTests` pins the fix (ADR-0482).
- [ ] The `Coap:RFC6347-4.2.7-1 BL-4674` line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`.
- [ ] `dotnet build` clean, fast tests green, the library at 100% line and branch coverage.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: ec597836 a91ff38c 2ea521fc fdc35bd6.
