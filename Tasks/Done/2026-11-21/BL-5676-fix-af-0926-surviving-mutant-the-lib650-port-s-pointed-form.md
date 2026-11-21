---
id: BL-5676
title: Fix AF-0926: Surviving mutant: the lib650 port's pointed form part can be sent two bytes longer
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary/Lib650.cs, Curl.Conformance.LibTest.UnitTests/Lib643To661Tests.cs]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-5676 — Fix AF-0926: Surviving mutant: the lib650 port's pointed form part can be sent two bytes longer

## Goal

The defect the audit office reported as AF-0926 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0926 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib650.cs:92`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib650.cs:92`

Mutant 'new(CurlFormOption.ContentsLength, (long)TestData.Length - 1)' -> '(long)TestData.Length + 1' survived (seed 276683 sample). Upstream lib650 passes strlen(testdata) - 1, so the part's contents length, and with it the multipart request bytes the port sends, change. No LibTest unit test checks the length or bytes of the second form part that BuildForm builds.

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/Lib650.cs line 92` as the evidence above describes (the mutant operator is `-1`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/Lib650.cs line 92`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by Lib650_AddsTheSixFieldsAndPostsOnlyWhenAllSucceed (new assertion on the second part's ContentsLength). Mutant applied: 5 of 8 cases failed; reverted: 1233 fast tests pass. touches widened to the test file Curl.Conformance.LibTest.UnitTests/Lib643To661Tests.cs. No production change, so no quality run.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing. Criteria at claim: 6f3bb35f 9742157d.
- 2026-11-21: Doing -> Done. Mutant killed by a new ContentsLength assertion in Lib650 test
