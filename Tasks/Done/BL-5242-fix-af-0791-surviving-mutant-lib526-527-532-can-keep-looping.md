---
id: BL-5242
title: Fix AF-0791: Surviving mutant: lib526/527/532 can keep looping after a select failure or a test timeout
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-11-15
completed: 2026-11-15
---
# BL-5242 — Fix AF-0791: Surviving mutant: lib526/527/532 can keep looping after a select failure or a test timeout

## Goal

The defect the audit office reported as AF-0791 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0791 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib526.cs:88`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib526.cs:88`

Mutant '!SelectTestAsync(...) || !steps.AbortOnTestTimeout(clock)' -> '... && ...' in TransferEachAsync survived. The mutant ends the loop only when both fail, so a select error alone or the test timeout alone no longer stops the program. A hung case would spin to the harness limit instead of ending with the program's error code. No test drives a failing select or an expired test clock through Lib526.

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/Lib526.cs line 88` as the evidence above describes (the mutant operator is `||`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/Lib526.cs line 88`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by Lib526Tests.TestAsync_ASelectThatFails_EndsTheLoopWithoutAdvancing (checked by hand: with && it fails, reverted passes). Added a sibling test for the expired clock; the mutant is the same line so the select test is the one that kills it.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing. Criteria at claim: 9d6136ac 9742157d.
- 2026-11-15: Doing -> Done. Select-failure test kills the Lib526 mutant
