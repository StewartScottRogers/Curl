---
id: BL-5653
title: Fix AF-0918: Surviving mutant: the lib583 port can refuse a run given exactly three arguments
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary/Lib583.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-5653 — Fix AF-0918: Surviving mutant: the lib583 port can refuse a run given exactly three arguments

## Goal

The defect the audit office reported as AF-0918 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0918 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib583.cs:24`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib583.cs:24`

`return context.Arguments.Count < 3 ? Task.FromResult(LibTestResult.FromCurlCode(LibTestErrorCodes.MajorBad)) : LibTestRun.WithMacroLibraryAsync(...)` mutated to `<= 3` survived Curl.Conformance.LibTest.UnitTests. With exactly three arguments the port then answers MajorBad without running, instead of running the transfer; no test runs lib583 at the boundary (Lib583_RemovesTheHandleAfterOnePerform passes more).

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/Lib583.cs line 24` as the evidence above describes (the mutant operator is `<`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/Lib583.cs line 24`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-20: Created.
