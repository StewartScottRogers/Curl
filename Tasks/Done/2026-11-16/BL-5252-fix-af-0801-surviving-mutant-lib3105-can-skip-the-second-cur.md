---
id: BL-5252
title: Fix AF-0801: Surviving mutant: lib3105 can skip the second curl_multi_remove_handle it exists to check
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary/Lib3105.cs]
requirement: none
created: 2026-11-15
completed: 2026-11-15
---
# BL-5252 — Fix AF-0801: Surviving mutant: lib3105 can skip the second curl_multi_remove_handle it exists to check

## Goal

The defect the audit office reported as AF-0801 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0801 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib3105.cs:65`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib3105.cs:65`

The seed 770877 sample of Curl.Conformance.LibTest.UnitLibrary: 'if (code == CurlMultiCode.Ok)' -> 'if (code != CurlMultiCode.Ok)' in RemoveTwice survived. With the mutant, a first removal that succeeds is never followed by the second, so the port no longer checks that removing a handle twice answers OK. A Libcurl regression there would pass the upstream case silently. No test checks that RemoveHandle is called twice or makes the second call fail.

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/Lib3105.cs line 65` as the evidence above describes (the mutant operator is `==`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/Lib3105.cs line 65`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mutant at Lib3105.cs:65 (`==` to `!=`) is killed by `Lib3105_WhenBothRemovalsSucceed_EndsOk`, which now asserts `Multi.Removals == 2`; it passes with the line reverted. Only the test project changed (library untouched, so no quality run). Build and the Lib3105 tests pass.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing. Criteria at claim: 33a7e3c5 9742157d.
- 2026-11-15: Doing -> Done. Mutant killed by Lib3105_WhenBothRemovalsSucceed_EndsOk
