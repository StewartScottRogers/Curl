---
id: BL-5241
title: Fix AF-0790: Surviving mutant: lib1560's UntilError can stop after the first passing row and skip every later check
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-11-15
completed:
---
# BL-5241 — Fix AF-0790: Surviving mutant: lib1560's UntilError can stop after the first passing row and skip every later check

## Goal

The defect the audit office reported as AF-0790 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0790 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1560Checks.cs:177`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1560Checks.cs:177`

Mutant 'if (errors > 0)' -> 'if (errors >= 0)' in UntilError survived (seed 5851, -MaxMutants 40). The mutant returns after the first row whatever its result, so lib1560 (upstream test1560, the URL API conformance program) checks one row of each table and reports success. URL-parsing regressions in later rows would then pass the upstream case unseen. No Lib1560ChecksTests case feeds a passing row followed by a failing one.

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/Lib1560Checks.cs line 177` as the evidence above describes (the mutant operator is `>`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/Lib1560Checks.cs line 177`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Killed by Lib1560ChecksTests.GetParts_PassingRowThenFailingRow_StillChecksTheLaterRow (verified by hand: mutant applied, test fails; reverted, all pass). Test-only change; no production code touched.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing. Criteria at claim: 50bca548 9742157d.
