---
id: BL-5722
title: Fix AF-0953: Surviving mutant: the lib1507 port can abort a loop at exactly its hang limit (> -> >=)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary/Lib1507.cs]
requirement: none
created: 2026-11-21
completed:
---
# BL-5722 — Fix AF-0953: Surviving mutant: the lib1507 port can abort a loop at exactly its hang limit (> -> >=)

## Goal

The defect the audit office reported as AF-0953 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0953 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1507.cs:95`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1507.cs:95`

Line 95: if (clock.DifferenceMilliseconds(clock.Now(), start) > MultiPerformHangTimeoutMilliseconds) writes "ABORTING TEST, since it seems that it would have run forever." and returns; mutated to >= survived. The loop takes an injected LibTestClock, so the boundary is testable, but no test runs it at exactly the limit: the port can abort, with that stderr line, one tick earlier than upstream.

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/Lib1507.cs line 95` as the evidence above describes (the mutant operator is `>`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/Lib1507.cs line 95`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Added `Lib1507_WhenAWaitEndsExactlyAtTheLimit_DoesNotAbort` (Lib1485To1511Tests.cs): it kills the `>` to `>=` mutant at line 95 (verified by hand: fails with the mutant, all 1239 fast tests pass reverted). Build clean.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing. Criteria at claim: 06445d9d 9742157d.
