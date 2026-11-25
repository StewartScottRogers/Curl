---
id: BL-5361
title: Fix AF-0841: Surviving mutant: lib2405 treats a failed multi init as a passed empty-multi test
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary/Lib2405.cs]
requirement: none
created: 2026-11-17
completed: 2026-11-17
---
# BL-5361 — Fix AF-0841: Surviving mutant: lib2405 treats a failed multi init as a passed empty-multi test

## Goal

The defect the audit office reported as AF-0841 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0841 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib2405.cs:161`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib2405.cs:161`

return false; -> return true; after !steps.MultiInit(library, out multi) survived. A failed curl_multi_init would then be reported as a passing check and the program would go on.

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/Lib2405.cs line 161` as the evidence above describes (the mutant operator is `false`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/Lib2405.cs line 161`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by `Lib2308To2504Tests.Lib2405_WhenTheMultiInitFails_StopsAtItWithoutRunningAnyTransfer` (mutant applied by hand at line 161: it failed; reverted: 1224 fast tests in Curl.Conformance.LibTest.UnitTests pass). Test-only change, no production code touched.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: 684bb6c3 9742157d.
- 2026-11-17: Doing -> Done. Mutant killed by new Lib2405 test
