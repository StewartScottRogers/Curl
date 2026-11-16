---
id: BL-5253
title: Fix AF-0802: Surviving mutant: lib573 can write its debug trace with hex dumps, unlike upstream's nohex trace
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary/Lib573.cs]
requirement: none
created: 2026-11-15
completed:
---
# BL-5253 — Fix AF-0802: Surviving mutant: lib573 can write its debug trace with hex dumps, unlike upstream's nohex trace

## Goal

The defect the audit office reported as AF-0802 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0802 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib573.cs:31`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib573.cs:31`

The seed 770877 sample of Curl.Conformance.LibTest.UnitLibrary: 'new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true)' -> 'noHex: false' in RunAsync survived. The program's standard-error trace then carries hex dumps that upstream's lib573 configuration leaves out. No test of Lib573 checks the trace text, so the configured trace format is unpinned.

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/Lib573.cs line 31` as the evidence above describes (the mutant operator is `true`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/Lib573.cs line 31`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing. Criteria at claim: ce743b0b 9742157d.
