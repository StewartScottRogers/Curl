---
id: BL-5362
title: Fix AF-0842: Surviving mutant: lib1564's too-early check has an untested boundary
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary/Lib1564.cs]
requirement: none
created: 2026-11-17
completed:
---
# BL-5362 — Fix AF-0842: Surviving mutant: lib1564's too-early check has an untested boundary

## Goal

The defect the audit office reported as AF-0842 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0842 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1564.cs:89`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1564.cs:89`

bool tooEarly = !endsEarly && waited < ThresholdMilliseconds; -> waited <= ThresholdMilliseconds survived. A poll returning after exactly the threshold would then be refused with 'curl_multi_poll returned too early' and TEST_ERR_MAJOR_BAD. The clock is an injected TimeProvider, so the boundary is testable, but Lib1564_OnAPollOnTheWrongSide_WritesItAndEndsMajorBad never hits it.

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/Lib1564.cs line 89` as the evidence above describes (the mutant operator is `<`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/Lib1564.cs line 89`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: d1663b1a 9742157d.
