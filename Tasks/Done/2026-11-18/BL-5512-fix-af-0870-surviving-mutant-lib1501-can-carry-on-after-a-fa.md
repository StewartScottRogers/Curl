---
id: BL-5512
title: Fix AF-0870: Surviving mutant: lib1501 can carry on after a failed first multi perform
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary/Lib1501.cs]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5512 — Fix AF-0870: Surviving mutant: lib1501 can carry on after a failed first multi perform

## Goal

The defect the audit office reported as AF-0870 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0870 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1501.cs:79`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1501.cs:79`

Mutant 'goesOn = goesOn && steps.AbortOnTestTimeout(...)' -> '||' survived Curl.Conformance.LibTest.UnitTests. When the first MultiPerformAsync fails (goesOn false), the mutant calls AbortOnTestTimeout, which answers true before the hang timeout, so the loop runs on with transfers still running instead of stopping with the failure's result. The lib1501 port's exit code and output can then differ from upstream, and no test makes its first perform fail.

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/Lib1501.cs line 79` as the evidence above describes (the mutant operator is `&&`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/Lib1501.cs line 79`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by new test `Lib1501_WhenTheFirstPerformFailsWithTransfersStillRunning_StopsBeforeTheLoop` (hand-applied `||` at line 79 made it fail; reverted, all 1227 pass). The existing failing-first-perform case returned running 0, so the loop never ran. Test-only change; build and fast tests green.

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing. Criteria at claim: c6f8bc9b 9742157d.
- 2026-11-18: Doing -> Done. Mutant killed by a new Lib1501 test
