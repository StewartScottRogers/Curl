---
id: BL-5560
title: Fix AF-0889: Surviving mutant: lib2032 can go on looping after curl_multi_perform fails
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary/Lib2032.cs]
requirement: none
created: 2026-11-19
completed: 2026-11-19
---
# BL-5560 — Fix AF-0889: Surviving mutant: lib2032 can go on looping after curl_multi_perform fails

## Goal

The defect the audit office reported as AF-0889 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0889 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib2032.cs:193`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib2032.cs:193`

Mutant return (false, running); -> return (true, running); survived all Curl.Conformance.LibTest.UnitTests (seed 446316). It sits in the turn that runs steps.MultiPerformAsync: when the perform step does not go on, the program should stop, but the mutant carries on into the next turn and writes its running/state line. No test makes lib2032's multi perform fail.

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/Lib2032.cs line 193` as the evidence above describes (the mutant operator is `false`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/Lib2032.cs line 193`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by the new test `Lib2032_WhenMultiPerformFails_StopsBeforeWritingTheRunningLine` (Lib1978To2306Tests.cs): with the mutant the loop went on into multi_fdset and wrote the `lib2032.c:181 new state` line; verified by hand, then reverted. Build clean, 1228 LibTest fast tests pass. Test-only change, no production code changed.

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing. Criteria at claim: e086ef68 9742157d.
- 2026-11-19: Doing -> Done. Added a test that kills the AF-0889 mutant
