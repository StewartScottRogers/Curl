---
id: BL-4564
title: Fix Coap conformance: Coap:RFC7252-5.4.1-1 and Coap:RFC7252-5.4.1-2 (reject a response carrying an unrecognised critical option)
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4525]
touches: [Curl.Extended.Protocol.Coap.UnitLibrary/Client/CoapExchange.cs, Curl.Extended.Protocol.Coap.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4564 — Fix Coap conformance: Coap:RFC7252-5.4.1-1 and Coap:RFC7252-5.4.1-2 (reject a response carrying an unrecognised critical option)

## Goal

Curl's `coap://` client rejects a response that carries an option it does not recognise of class critical (an odd option number), as RFC 7252 section 5.4.1 requires, so the cases `Coap:RFC7252-5.4.1-1` and `Coap:RFC7252-5.4.1-2` pass.

## Context

Found by BL-4525's cases in `Curl.Conformance.Extended.UnitTests/Coap/RequestResponse/CoapOptionHandlingTests.cs`. RFC 7252 section 5.4.1: "Unrecognized options of class 'critical' that occur in a Confirmable response, or piggybacked in an Acknowledgement, MUST cause the response to be rejected (Section 4.2). Unrecognized options of class 'critical' that occur in a Non-confirmable message MUST cause the message to be rejected (Section 4.3)." Rejecting an Acknowledgement is silently ignoring it; rejecting a Confirmable message is answering it with a matching Reset; rejecting a Non-confirmable message is ignoring it (a Reset MAY be sent). Today `CoapExchange.Classify` (`Curl.Extended.Protocol.Coap.UnitLibrary/Client/CoapExchange.cs`) takes any response whose token matches and curl writes its payload. The options curl recognises in a response are those in `Wire/CoapOptionNumber.cs` (Observe, Block2, Block1, Content-Format, Max-Age, ETag, Location-*, Size1, Size2 and the like); an option number it does not recognise that is odd is critical.

## Acceptance criteria

- [x] `CoapOptionHandlingTests.PiggybackedResponseWithUnrecognisedCriticalOption_IsIgnored`, `CoapOptionHandlingTests.ConfirmableResponseWithUnrecognisedCriticalOption_IsRejectedWithAReset` and `CoapOptionHandlingTests.NonConfirmableResponseWithUnrecognisedCriticalOption_IsRejected` pass.
- [x] A focused unit test in `Curl.Extended.Protocol.Coap.UnitTests` pins the fix (ADR-0482).
- [x] The `Coap:RFC7252-5.4.1-1` and `Coap:RFC7252-5.4.1-2` lines are removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`; the known-failures check passes.
- [x] `Curl.Extended.Protocol.Coap.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Delivered directly (one-file change, no architect stage needed): `CoapExchange.Classify` now turns a response (piggybacked or separate) that carries an odd-numbered option outside the registered set in `CoapOptionNumber` into `Reply.Ignored`, so the existing answer path resets it when Confirmable and silently drops it when an ACK or Non-confirmable (RFC 7252 section 5.4.1, 4.2, 4.3). Choice: "recognised" means every option number the client registers, request-only ones included, since the client knows them; no Reset for Non-confirmable (MAY, and the conformance case allows either).
- Focused test: `CoapProtocolHandlerTests.ExecuteAsync_ResponsesWithAnUnrecognisedCriticalOption_AreRejectedAndOnlyElectiveOrRecognisedOptionsAccepted`.
- Measure-CodeQuality: Coap library 100% line, 100% branch, 0 failing members. Full solution build clean with -warnaserror; fast tests green (115 test assemblies). Platform-neutral: no file system, path or OS text involved.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: fa5cfe4d a91ff38c 7872daae 44307beb 914c30cc.
- 2026-11-02: Doing -> Done. coap:// rejects responses carrying an unrecognised critical option (RFC 7252 5.4.1)
