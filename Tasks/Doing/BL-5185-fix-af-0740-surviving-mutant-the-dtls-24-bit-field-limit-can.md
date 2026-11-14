---
id: BL-5185
title: Fix AF-0740: Surviving mutant: the DTLS 24-bit field limit can be raised past 24 bits; the tests derive their bound from the same constant
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Coap.UnitLibrary]
requirement: none
created: 2026-11-14
completed:
---
# BL-5185 — Fix AF-0740: Surviving mutant: the DTLS 24-bit field limit can be raised past 24 bits; the tests derive their bound from the same constant

## Goal

The defect the audit office reported as AF-0740 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0740 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.Coap.UnitLibrary/Dtls/Wire/DtlsHandshakeHeader.cs:22`

Location: `Curl.Extended.Protocol.Coap.UnitLibrary/Dtls/Wire/DtlsHandshakeHeader.cs:22`

Mutant public const int MaximumUInt24 = (1 << 24) - 1 -> (1 << 24) + 1 survived in both runs (seed 641133). Encode then accepts lengths and offsets of 2^24 and 2^24+1 and writes them truncated into 3 bytes (RFC 6347 section 4.2.2). Encode_Rfc6347Section4_2_2_RejectsFieldsOutOfRange and Split_Rfc6347Section4_2_2_RejectsABodyPast24Bits compute their out-of-range value as DtlsHandshakeHeader.MaximumUInt24 + 1, so they move with the mutated constant and cannot catch it; no test pins 16777215.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.Coap.UnitLibrary/Dtls/Wire/DtlsHandshakeHeader.cs line 22` as the evidence above describes (the mutant operator is `-1`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.Coap.UnitLibrary/Dtls/Wire/DtlsHandshakeHeader.cs line 22`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing. Criteria at claim: 3c5b1ece 9742157d.
