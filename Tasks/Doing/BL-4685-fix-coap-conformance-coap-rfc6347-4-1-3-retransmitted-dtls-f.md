---
id: BL-4685
title: Fix Coap conformance: Coap:RFC6347-4.1-3 retransmitted DTLS flight under new record sequence numbers
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Coap.UnitLibrary/Dtls/Retransmission/DtlsFlightRetransmitter.cs, Curl.Extended.Protocol.Coap.UnitTests/Dtls/Retransmission, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-03
completed:
---
# BL-4685 — Fix Coap conformance: Coap:RFC6347-4.1-3 retransmitted DTLS flight under new record sequence numbers

## Goal

Curl's `coaps://` client sends a retransmitted DTLS flight under new record sequence numbers.

## Context

Found by BL-4574. RFC 6347 section 4.1 (and 4.1.2.6): each record has its own sequence number in its epoch, so a flight sent again verbatim is discarded by the peer's replay window. `DtlsFlightRetransmitter` resends the flight's datagrams byte for byte, so the retransmitted ClientHello carries the sequence number of the first. Epoch 1 records (the Finished) need resealing under the new number too.

## Acceptance criteria

- [ ] `CoapDtlsRetransmissionTests.HelloFlightLost_TheRetransmittedClientHelloGoesUnderANewRecordSequenceNumber` passes.
- [ ] A focused unit test in `Curl.Extended.Protocol.Coap.UnitTests` pins the fix (ADR-0482).
- [ ] The `Coap:RFC6347-4.1-3 BL-4675` line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`.
- [ ] `dotnet build` clean, fast tests green, the library at 100% line and branch coverage.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 4772c294 a91ff38c cab5d64d fdc35bd6.
