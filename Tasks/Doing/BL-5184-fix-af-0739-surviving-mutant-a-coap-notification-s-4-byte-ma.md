---
id: BL-5184
title: Fix AF-0739: Surviving mutant: a CoAP notification's 4-byte Max-Age can be ignored for the 60-second default
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Coap.UnitLibrary]
requirement: none
created: 2026-11-14
completed:
---
# BL-5184 — Fix AF-0739: Surviving mutant: a CoAP notification's 4-byte Max-Age can be ignored for the 60-second default

## Goal

The defect the audit office reported as AF-0739 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0739 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.Coap.UnitLibrary/Observe/CoapObservation.cs:101`

Location: `Curl.Extended.Protocol.Coap.UnitLibrary/Observe/CoapObservation.cs:101`

Mutant { Value.Length: <= MaximumMaxAgeLength } -> < survived in both runs (seed 641133). A Max-Age option of exactly 4 bytes (RFC 7252 allows 0-4) would fall back to DefaultMaxAge (60 s), so --observe re-registers on the wrong schedule. The only test, MaxAgeOf_AValueLongerThanFourBytes_GivesTheDefault, uses 5 bytes; no test decodes a 4-byte Max-Age.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.Coap.UnitLibrary/Observe/CoapObservation.cs line 101` as the evidence above describes (the mutant operator is `<=`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.Coap.UnitLibrary/Observe/CoapObservation.cs line 101`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by new test `MaxAgeOf_AFourByteValue_DecodesIt` (mutant `<` fails it; reverted, 310 pass). Test-only change; production code untouched. Fast tests of the Coap UnitTests project green.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing. Criteria at claim: afd26901 9742157d.
