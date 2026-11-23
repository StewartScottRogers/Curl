---
id: BL-5792
title: Fix AF-0990: Surviving mutant: the lib1542 port's debug trace can stamp each line with the time (traceTime false -> true)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary/Lib1542.cs]
requirement: none
created: 2026-11-22
completed:
---
# BL-5792 — Fix AF-0990: Surviving mutant: the lib1542 port's debug trace can stamp each line with the time (traceTime false -> true)

## Goal

The defect the audit office reported as AF-0990 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0990 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1542.cs:49`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1542.cs:49`

Seeded mutation run: 'new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: false, noHex: true)' -> 'traceTime: true' survived. The port writes upstream's trace on standard error; with the mutant every trace line gets a timestamp prefix that upstream's lib1542 does not write. This is the same family as AF-0978 (noHex), but a different argument, and no test checks lib1542's trace lines.

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/Lib1542.cs line 49` as the evidence above describes (the mutant operator is `false`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/Lib1542.cs line 49`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-22: Created.
