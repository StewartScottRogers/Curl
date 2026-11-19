---
id: BL-5538
title: Fix AF-0874: Surviving mutant: an AMQP content header with reserved property flag bit 1 set can crash the reader
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Amqp0.UnitLibrary/Wire/Amqp0ContentHeader.cs]
requirement: none
created: 2026-11-19
completed:
---
# BL-5538 — Fix AF-0874: Surviving mutant: an AMQP content header with reserved property flag bit 1 set can crash the reader

## Goal

The defect the audit office reported as AF-0874 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0874 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.Amqp0.UnitLibrary/Wire/Amqp0ContentHeader.cs:66`

Location: `Curl.Extended.Protocol.Amqp0.UnitLibrary/Wire/Amqp0ContentHeader.cs:66`

Mutant 'for (int index = 0; index <= PropertiesInFlagOrder.Length; index++)' (from '<') survived in the seeded sample (seed 735398) and again in the targeted -Site run. PropertiesInFlagOrder has 14 entries (bits 15..2, FlagBit(i) = 1 << (15 - i)). Under the mutant, a header whose reserved bit 1 is set indexes PropertiesInFlagOrder[14] and throws IndexOutOfRangeException. The original ignores the bit and goes on to EnsureAtEnd. So a broker frame that curl now reads, or refuses as a syntax error, would end the transfer with an unhandled exception and a different exit code. Tests cover bit 0 ('more property flags') but no header with bit 1 set.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.Amqp0.UnitLibrary/Wire/Amqp0ContentHeader.cs line 66` as the evidence above describes (the mutant operator is `<`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.Amqp0.UnitLibrary/Wire/Amqp0ContentHeader.cs line 66`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-19: Created.
