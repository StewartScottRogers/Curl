---
id: BL-4538
title: Fix Coap conformance: Coap:RFC7252-4.2-5
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Coap.UnitLibrary/Client/CoapExchange.cs, Curl.Conformance.Extended.UnitLibrary/Harness/ReferenceDatagramChannel.cs, Curl.Extended.Protocol.Coap.UnitTests/Client, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4538 — Fix Coap conformance: Coap:RFC7252-4.2-5

## Goal

After ignoring a malformed Acknowledgement (RFC 7252 sections 3, 4.1 and 4.2: unknown version, token length 9, payload marker with no payload, option nibble 15, Empty message with bytes), curl takes the well-formed piggybacked response to its retransmission and exits 0.

## Context

Found by BL-4524: `CoapMessageFormatTests.MalformedAcknowledgement_IsSilentlyIgnoredAndTheRequestRetransmitted` (all six rows) answers curl's first datagram with a malformed ACK echoing its Message ID and every later one with a good piggybacked 2.05; curl retransmits 4 times and exits 28 ("CoAP server did not answer after 4 retransmissions"), so the good answers are not taken. A silent server (`CoapRetransmissionTests`) and a separate response (`CoapConfirmableExchangeTests`) behave, so start in `CoapExchange.AwaitResponseAsync` at the path after a datagram classified `Reply.Ignored`, and in how the datagram channel under it delivers a later datagram.

## Acceptance criteria

- [x] `CoapMessageFormatTests.MalformedAcknowledgement_IsSilentlyIgnoredAndTheRequestRetransmitted` passes for every row.
- [x] A focused unit test in `Curl.Extended.Protocol.Coap.UnitTests/Client` pins the fix (ADR-0482).
- [x] The `Coap:RFC7252-4.2-5 BL-4527` line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`.

## Notes

- Cause: not `CoapExchange`. The new unit test (`CoapProtocolHandlerTests.ExecuteAsync_AMalformedAcknowledgement_IsIgnoredAndThePiggybackedResponseToTheRetransmissionTaken`, all six rows) passed before any change, so the exchange already ignores a malformed ACK and takes the answer to its retransmission. With the KnownFailures line removed, the conformance server logged five identical requests at 0, ~2.2, ~6.5, ~15, ~33 s with good answers to the last four. `TimerJumpingClock` fires each retransmission timer inline on the run loop's thread. The exchange then retransmits and `ReferenceDatagramChannel` queues the answer, but its unbounded channel completed the waiting read on the thread pool. So the loop fired all four timers before curl saw any answer: a race in the harness, which row passed was luck.
- Fix: `ReferenceDatagramChannel` creates its channel with `AllowSynchronousContinuations = true`, so a waiting receive completes inside `SendAsync` and the exchange's next `WhenAny` sees the answer before the next timer. `touches` widened to `Curl.Conformance.Extended.UnitLibrary/Harness/ReferenceDatagramChannel.cs` for this. No task in Doing on origin/work/dark-factory names it. `CoapExchange.cs` is unchanged. No ADR, since this is a test-harness fix and no behaviour decision.
- Verified: dotnet build clean, fast tests green (115 test projects), Measure-CodeQuality.ps1 -Library Curl.Conformance.Extended.UnitLibrary 0 failing members; the change is platform-neutral (no paths, OS text or certificates).
- The focused unit test pins the client behaviour in the Coap library (ADR-0482), so a regression there is caught without the conformance run.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 7f1cdf75 081c5817 752977b7.
- 2026-11-02: Doing -> Done. Coap:RFC7252-4.2-5 passes: curl takes the good answer to its retransmission after a malformed ACK; the harness race that hid it is fixed
