---
id: BL-5360
title: Fix AF-0840: Surviving mutant: lib2023 can carry on to its next requests after a failed easy init
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary/Lib2023.cs]
requirement: none
created: 2026-11-17
completed:
---
# BL-5360 — Fix AF-0840: Surviving mutant: lib2023 can carry on to its next requests after a failed easy init

## Goal

The defect the audit office reported as AF-0840 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0840 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib2023.cs:95`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib2023.cs:95`

return false; -> return true; after !steps.EasyInitOrMajorBad(...) survived. The caller then runs the next request block on a dead library instead of stopping, so the program's result and request count can change. No test drives a failing EasyInit through RequestsOnOneHandleAsync.

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/Lib2023.cs line 95` as the evidence above describes (the mutant operator is `false`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/Lib2023.cs line 95`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-17: Created.
