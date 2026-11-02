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
completed:
---
# BL-4564 — Fix Coap conformance: Coap:RFC7252-5.4.1-1 and Coap:RFC7252-5.4.1-2 (reject a response carrying an unrecognised critical option)

## Goal

Curl's `coap://` client rejects a response that carries an option it does not recognise of class critical (an odd option number), as RFC 7252 section 5.4.1 requires, so the cases `Coap:RFC7252-5.4.1-1` and `Coap:RFC7252-5.4.1-2` pass.

## Context

Found by BL-4525's cases in `Curl.Conformance.Extended.UnitTests/Coap/RequestResponse/CoapOptionHandlingTests.cs`. RFC 7252 section 5.4.1: "Unrecognized options of class 'critical' that occur in a Confirmable response, or piggybacked in an Acknowledgement, MUST cause the response to be rejected (Section 4.2). Unrecognized options of class 'critical' that occur in a Non-confirmable message MUST cause the message to be rejected (Section 4.3)." Rejecting an Acknowledgement is silently ignoring it; rejecting a Confirmable message is answering it with a matching Reset; rejecting a Non-confirmable message is ignoring it (a Reset MAY be sent). Today `CoapExchange.Classify` (`Curl.Extended.Protocol.Coap.UnitLibrary/Client/CoapExchange.cs`) takes any response whose token matches and curl writes its payload. The options curl recognises in a response are those in `Wire/CoapOptionNumber.cs` (Observe, Block2, Block1, Content-Format, Max-Age, ETag, Location-*, Size1, Size2 and the like); an option number it does not recognise that is odd is critical.

## Acceptance criteria

- [ ] `CoapOptionHandlingTests.PiggybackedResponseWithUnrecognisedCriticalOption_IsIgnored`, `CoapOptionHandlingTests.ConfirmableResponseWithUnrecognisedCriticalOption_IsRejectedWithAReset` and `CoapOptionHandlingTests.NonConfirmableResponseWithUnrecognisedCriticalOption_IsRejected` pass.
- [ ] A focused unit test in `Curl.Extended.Protocol.Coap.UnitTests` pins the fix (ADR-0482).
- [ ] The `Coap:RFC7252-5.4.1-1` and `Coap:RFC7252-5.4.1-2` lines are removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`; the known-failures check passes.
- [ ] `Curl.Extended.Protocol.Coap.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: fa5cfe4d a91ff38c 7872daae 44307beb 914c30cc.
