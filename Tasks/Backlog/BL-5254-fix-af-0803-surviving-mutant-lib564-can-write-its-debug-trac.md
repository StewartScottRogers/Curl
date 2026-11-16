---
id: BL-5254
title: Fix AF-0803: Surviving mutant: lib564 can write its debug trace without the time stamps upstream's trace has
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-11-15
completed:
---
# BL-5254 — Fix AF-0803: Surviving mutant: lib564 can write its debug trace without the time stamps upstream's trace has

## Goal

The defect the audit office reported as AF-0803 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0803 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib564.cs:66`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib564.cs:66`

The seed 770877 sample of Curl.Conformance.LibTest.UnitLibrary: 'new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true)' -> 'traceTime: false' in TestAsync survived. The program's standard-error trace lines then lose their time prefix. No test of Lib564 checks the trace text, so the configured trace format is unpinned.

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/Lib564.cs line 66` as the evidence above describes (the mutant operator is `true`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/Lib564.cs line 66`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-15: Created.
