---
id: BL-4685
title: Fix Coap conformance: Coap:RFC6347-4.1-3 retransmitted DTLS flight under new record sequence numbers
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4684]
touches: [Curl.Extended.Protocol.Coap.UnitLibrary/Dtls/Retransmission/DtlsFlightRetransmitter.cs, Curl.Extended.Protocol.Coap.UnitLibrary/Dtls/Handshake/DtlsClientHandshake.cs, Curl.Extended.Protocol.Coap.UnitTests/Dtls/Retransmission, Curl.Extended.Protocol.Coap.UnitTests/Dtls/TestServer/DtlsTestServer.cs, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4685 — Fix Coap conformance: Coap:RFC6347-4.1-3 retransmitted DTLS flight under new record sequence numbers

## Goal

Curl's `coaps://` client sends a retransmitted DTLS flight under new record sequence numbers.

## Context

Found by BL-4574. RFC 6347 section 4.1 (and 4.1.2.6): each record has its own sequence number in its epoch, so a flight sent again verbatim is discarded by the peer's replay window. `DtlsFlightRetransmitter` resends the flight's datagrams byte for byte, so the retransmitted ClientHello carries the sequence number of the first. Epoch 1 records (the Finished) need resealing under the new number too.

## Acceptance criteria

- [x] `CoapDtlsRetransmissionTests.HelloFlightLost_TheRetransmittedClientHelloGoesUnderANewRecordSequenceNumber` passes.
- [x] A focused unit test in `Curl.Extended.Protocol.Coap.UnitTests` pins the fix (ADR-0482).
- [x] The `Coap:RFC6347-4.1-3 BL-4675` line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`.
- [x] `dotnet build` clean, fast tests green, the library at 100% line and branch coverage.

## Notes

- 2026-11-03 (lane 5): The fix needs `Dtls/Handshake/DtlsClientHandshake.cs` too. It builds each flight once as sealed record bytes (`Epoch0Record` takes the next epoch 0 sequence number, `protector.Protect` the next epoch 1 one), so the retransmitter cannot renumber them by itself. Plan: `DtlsFlightRetransmitter.SendFlightAsync` takes a `Func<IReadOnlyList<ReadOnlyMemory<byte>>>` that seals the flight afresh on each send. `DtlsClientHandshake` passes lambdas wrapping its `Epoch0Record` and `Protect` calls, and keeps the handshake message_seq unchanged, as RFC 6347 section 4.2.2 requires. Update the existing `DtlsFlightRetransmitterTests` for the new signature. Add a unit test showing that each resend calls the sealer again. BL-4684, in Doing, holds `DtlsClientHandshake.cs`, so this run added that file to `touches` and BL-4684 to `depends-on`, and sent the task back to Backlog without changing any code (rule 3).

- 2026-11-03 (lane 2): Done. `DtlsFlightRetransmitter.SendFlightAsync` now takes a sealer (`Func<IReadOnlyList<ReadOnlyMemory<byte>>>`) called once per send, and `DtlsClientHandshake` passes lambdas around `Epoch0Record` and `protector.Protect`, so every resend takes the next record sequence number of its epoch while the handshake message_seq stays the same (RFC 6347 sections 4.1 and 4.2.2). Focused test: `DtlsFlightRetransmitterTests.SendFlightAsync_Rfc6347Section4_1_SealsTheFlightAfreshForEachResendSoEachGoesUnderANewSequenceNumber`. Added `Curl.Extended.Protocol.Coap.UnitTests/Dtls/TestServer/DtlsTestServer.cs` to touches (no task in Doing names it): the test server only recognised a resent Finished as a byte-for-byte replay, so it now answers a Finished arriving after the handshake completed with its Finished flight again, as RFC 6347 section 4.2.4 asks. Measure-CodeQuality for the Coap library: 0 failing members. CI on Linux and macOS: the change is platform-neutral (no paths, OS errors or certificates) and the fast tests are green here.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 4772c294 a91ff38c cab5d64d fdc35bd6.
- 2026-11-03: Doing -> Backlog. Needs Curl.Extended.Protocol.Coap.UnitLibrary/Dtls/Handshake/DtlsClientHandshake.cs, which BL-4684 (in Doing) holds; waits on BL-4684
- 2026-11-03: Backlog -> Doing. Criteria at claim: 4772c294 a91ff38c cab5d64d fdc35bd6.
- 2026-11-03: Doing -> Done. Retransmitted DTLS flights are resealed under new record sequence numbers
