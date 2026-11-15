---
id: BL-5228
title: Fix AF-0777: Surviving mutant: telnet -v can print 'RCVD IAC 236' instead of 'RCVD IAC EOF'
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Telnet.UnitLibrary]
requirement: none
created: 2026-11-15
completed:
---
# BL-5228 — Fix AF-0777: Surviving mutant: telnet -v can print 'RCVD IAC 236' instead of 'RCVD IAC EOF'

## Goal

The defect the audit office reported as AF-0777 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0777 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Original.Protocol.Telnet.UnitLibrary/TelnetTraceReporter.cs:87`

Location: `Curl.Original.Protocol.Telnet.UnitLibrary/TelnetTraceReporter.cs:87`

events.ReportInfo("RCVD IAC " + (command >= FirstNamedCommand ? CommandNames[command - FirstNamedCommand] : command.ToString(...))) with FirstNamedCommand = 236; mutated to command > FirstNamedCommand survived. IAC 236 (EOF) then prints its number instead of curl's name, which changes the -v bytes on stderr. The trace tests cover IAC 7 and IAC NOP (241) but not the first named command.

Reproduction: the audit office's mutation test changed `Curl.Original.Protocol.Telnet.UnitLibrary/TelnetTraceReporter.cs line 87` as the evidence above describes (the mutant operator is `>=`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Original.Protocol.Telnet.UnitLibrary/TelnetTraceReporter.cs line 87`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-15: Created.
