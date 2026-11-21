---
id: BL-5654
title: Fix AF-0919: Surviving mutant: the lib1515 port can go on after easy_init fails instead of stopping
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary/Lib1515.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-5654 — Fix AF-0919: Surviving mutant: the lib1515 port can go on after easy_init fails instead of stopping

## Goal

The defect the audit office reported as AF-0919 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0919 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1515.cs:119`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1515.cs:119`

In Request.PerformAsync, `if (!steps.EasyInit(library, out CurlEasy? easy)) { return false; }` mutated to `return true;` survived Curl.Conformance.LibTest.UnitTests. The port then reports the failed request as done and carries on with the next one, where upstream's do_one_request stops; the program's result and stderr differ. No test drives lib1515 with an easy_init that fails.

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/Lib1515.cs line 119` as the evidence above describes (the mutant operator is `false`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/Lib1515.cs line 119`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-20: Created.
