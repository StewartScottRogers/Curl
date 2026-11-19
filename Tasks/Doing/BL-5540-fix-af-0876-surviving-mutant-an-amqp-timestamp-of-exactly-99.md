---
id: BL-5540
title: Fix AF-0876: Surviving mutant: an AMQP timestamp of exactly 9999-12-31T23:59:59Z can be refused
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Amqp0.UnitLibrary/Wire/Amqp0Reader.cs]
requirement: none
created: 2026-11-19
completed:
---
# BL-5540 — Fix AF-0876: Surviving mutant: an AMQP timestamp of exactly 9999-12-31T23:59:59Z can be refused

## Goal

The defect the audit office reported as AF-0876 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0876 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.Amqp0.UnitLibrary/Wire/Amqp0Reader.cs:39`

Location: `Curl.Extended.Protocol.Amqp0.UnitLibrary/Wire/Amqp0Reader.cs:39`

Mutant 'if (seconds is < -62135596800 or >= 253402300799)' (from '>') survived in the sample and in the targeted -Site run. 253402300799 is the last second of year 9999, a valid DateTimeOffset. Under the mutant it is refused as a syntax error. No test reads the upper boundary.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.Amqp0.UnitLibrary/Wire/Amqp0Reader.cs line 39` as the evidence above describes (the mutant operator is `>`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.Amqp0.UnitLibrary/Wire/Amqp0Reader.cs line 39`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing. Criteria at claim: 6c075cd5 9742157d.
