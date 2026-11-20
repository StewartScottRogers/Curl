---
id: BL-5624
title: Fix AF-0909: Surviving mutant: the lib2412 port can trace without the time prefix
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary/Lib2412.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-5624 — Fix AF-0909: Surviving mutant: the lib2412 port can trace without the time prefix

## Goal

The defect the audit office reported as AF-0909 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0909 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib2412.cs:54`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib2412.cs:54`

Invoke-MutationTest -Library Curl.Conformance.LibTest.UnitLibrary -MaxMutants 40 -Seed 912464: 'new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true)' mutated to traceTime: false survived. Each trace line then loses its 'HH:mm:ss.ffffff ' prefix (LibTestDebugTrace.cs:130), so the port's standard error changes and no test pins it.

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/Lib2412.cs line 54` as the evidence above describes (the mutant operator is `true`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/Lib2412.cs line 54`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by `Lib2308To2504Tests.Lib2412_TraceCallback_StartsEachLineWithTheTime` (calls the registered debug callback after the run, expects the `00:00:00.000000 ` prefix). Mutant applied by hand: that test failed, others passed; reverted, all 1230 LibTest fast tests pass. No production change.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing. Criteria at claim: 79c482fc 9742157d.
