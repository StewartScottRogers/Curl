---
id: BL-5539
title: Fix AF-0875: Surviving mutant: an AMQP timestamp of exactly 0001-01-01T00:00:00Z can be refused
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Amqp0.UnitLibrary/Wire/Amqp0Reader.cs]
requirement: none
created: 2026-11-19
completed:
---
# BL-5539 — Fix AF-0875: Surviving mutant: an AMQP timestamp of exactly 0001-01-01T00:00:00Z can be refused

## Goal

The defect the audit office reported as AF-0875 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0875 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.Amqp0.UnitLibrary/Wire/Amqp0Reader.cs:39`

Location: `Curl.Extended.Protocol.Amqp0.UnitLibrary/Wire/Amqp0Reader.cs:39`

Mutant 'if (seconds is <= -62135596800 or > 253402300799)' (from '<') survived in the sample and in the targeted -Site run. -62135596800 is the first second of year 1, which DateTimeOffset represents. Under the mutant a message whose timestamp is that value is refused with a syntax error, failing the transfer. Timestamp_OutsideDateTimeOffset_IsSyntaxError tests values outside the range, but no test reads the lower boundary itself.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.Amqp0.UnitLibrary/Wire/Amqp0Reader.cs line 39` as the evidence above describes (the mutant operator is `<`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.Amqp0.UnitLibrary/Wire/Amqp0Reader.cs line 39`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-19: Created.
