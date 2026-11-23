---
id: BL-5794
title: Fix AF-0992: Surviving mutant: lib530's completion check can start out as success (success = false -> true), hiding 'Error getting file.'
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary/Lib530Run.cs]
requirement: none
created: 2026-11-22
completed:
---
# BL-5794 — Fix AF-0992: Surviving mutant: lib530's completion check can start out as success (success = false -> true), hiding 'Error getting file.'

## Goal

The defect the audit office reported as AF-0992 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0992 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib530Run.cs:209`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib530Run.cs:209`

Seeded mutation run: in CheckForCompletion (t530_checkForCompletion), 'success = false;' -> 'success = true;' survived. When no message is read, the mutant reports success, so the caller's 'if (!success) { WriteMessage("Error getting file."); steps.Result = MajorBad; }' is skipped on that path. No test drives a completion check that reads no message.

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/Lib530Run.cs line 209` as the evidence above describes (the mutant operator is `false`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/Lib530Run.cs line 209`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: 02a45221 9742157d.
