---
id: BL-5788
title: Fix AF-0986: Surviving mutant: the telnet trace names option 40 from a 40-entry table (>= -> >), throwing IndexOutOfRangeException
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Telnet.UnitLibrary/TelnetTraceReporter.cs]
requirement: none
created: 2026-11-22
completed:
---
# BL-5788 — Fix AF-0986: Surviving mutant: the telnet trace names option 40 from a 40-entry table (>= -> >), throwing IndexOutOfRangeException

## Goal

The defect the audit office reported as AF-0986 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0986 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Original.Protocol.Telnet.UnitLibrary/TelnetTraceReporter.cs:192`

Location: `Curl.Original.Protocol.Telnet.UnitLibrary/TelnetTraceReporter.cs:192`

Seeded mutation run (seed 718137, 25 mutants): 'if (option >= FirstUnnamedOption)' -> 'if (option > FirstUnnamedOption)' survived. FirstUnnamedOption is 40 and OptionNames holds options 0-39. With the mutant, a subnegotiation of option 40 skips the '40 (unknown)' branch and reads OptionNames[40], which throws IndexOutOfRangeException during the -v trace and ends the transfer. No Telnet test traces a subnegotiation of exactly option 40.

Reproduction: the audit office's mutation test changed `Curl.Original.Protocol.Telnet.UnitLibrary/TelnetTraceReporter.cs line 192` as the evidence above describes (the mutant operator is `>=`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Original.Protocol.Telnet.UnitLibrary/TelnetTraceReporter.cs line 192`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: 037003ed 9742157d.
