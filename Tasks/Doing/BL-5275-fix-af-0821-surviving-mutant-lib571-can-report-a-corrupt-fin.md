---
id: BL-5275
title: Fix AF-0821: Surviving mutant: lib571 can report a corrupt final full-size RTP chunk with the wrong message
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary/Lib571.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5275 — Fix AF-0821: Surviving mutant: lib571 can report a corrupt final full-size RTP chunk with the wrong message

## Goal

The defect the audit office reported as AF-0821 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0821 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib571.cs:148`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib571.cs:148`

`if (remaining > RtpData.Length)` (upstream lib571.c's `message_size - i > RTP_DATA_SIZE`) changed to `>=` survived. A corrupted last chunk of exactly RTP_DATA_SIZE bytes then prints 'RTP PAYLOAD CORRUPTED' instead of 'RTP PAYLOAD END CORRUPTED (n)', changing the port's output. No test corrupts a final full-size chunk.

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/Lib571.cs line 148` as the evidence above describes (the mutant operator is `>`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/Lib571.cs line 148`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: 80e6f9b2 9742157d.
