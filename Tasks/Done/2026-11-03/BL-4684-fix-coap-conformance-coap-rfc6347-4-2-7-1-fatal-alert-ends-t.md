---
id: BL-4684
title: Fix Coap conformance: Coap:RFC6347-4.2.7-1 fatal alert ends the DTLS handshake
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Coap.UnitLibrary/Dtls/Handshake/DtlsClientHandshake.cs, Curl.Extended.Protocol.Coap.UnitLibrary/CLAUDE.md, Curl.Extended.Protocol.Coap.UnitTests/Dtls/Handshake, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4684 — Fix Coap conformance: Coap:RFC6347-4.2.7-1 fatal alert ends the DTLS handshake

## Goal

Curl's `coaps://` client ends the DTLS handshake at once with exit 35 when the server sends a fatal alert, and sends nothing more.

## Context

Found by BL-4574. RFC 6347 section 4.2.7 and RFC 5246 section 7.2: on a fatal alert the connection ends. `DtlsClientHandshake.ReadServerFlightAsync` keeps only Handshake records, so it ignores the alert, retransmits its ClientHello until the timer gives up and only then exits 35. Case: `CoapDtlsAlertTests` (sends 2 records expected, more seen).

## Acceptance criteria

- [x] `CoapDtlsAlertTests.FatalAlertInPlaceOfTheHelloFlight_CurlStopsWithExit35AndSendsNothingMore` passes.
- [x] A focused unit test in `Curl.Extended.Protocol.Coap.UnitTests` pins the fix (ADR-0482).
- [x] The `Coap:RFC6347-4.2.7-1 BL-4674` line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`.
- [x] `dotnet build` clean, fast tests green, the library at 100% line and branch coverage.

## Notes

- Delivered directly rather than through the full `/feature` stages: a one-method fix whose behaviour the RFC fixes. `DtlsClientHandshake.AddHandshakeFragments` now passes an opened Alert record to `EndOnFatalAlert`, which throws exit 35 ("DTLS handshake failed: the server sent the fatal alert <description>") for level 2; the reader task faults, so `DtlsFlightRetransmitter` returns at once and nothing more is sent. A warning alert, or one shorter than two bytes, is ignored (sensible default: RFC 5246 section 7.2 lets the handshake go on after a warning). The same path covers a protected alert in place of the server's Finished.
- `RunAsync_Rfc6347Section4_1_2_7_RecordsItCannotUse_...` delivered an epoch-0 fatal alert it expected dropped; it now delivers it at epoch 1 (a record the client cannot open), as an epoch-0 fatal alert now rightly ends the handshake.
- New unit tests: `RunAsync_Rfc6347Section4_2_7_FatalAlertInPlaceOfTheHelloFlight_FailsWithExit35AndSendsNothingMore` and `RunAsync_Rfc5246Section7_2_WarningOrTruncatedAlert_IsIgnoredAndTheHandshakeCompletes`.
- The KnownFailures line was `Coap:RFC6347-4.2.7-1 BL-4684` (the criterion says BL-4674); that line is removed.
- Touches widened by `Curl.Extended.Protocol.Coap.UnitLibrary/CLAUDE.md` to keep its handshake description true; no task in Doing on origin/work/dark-factory names it.
- Measured: Coap library 296 tests green, Measure-CodeQuality 100% line and branch, 0 failing members; full build 0 warnings, fast tests green. Platform-neutral (no paths or OS text).

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: ec597836 a91ff38c 2ea521fc fdc35bd6.
- 2026-11-03: Doing -> Done. coaps:// ends the DTLS handshake with exit 35 on a fatal alert and sends nothing more
