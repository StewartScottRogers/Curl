---
id: BL-5695
title: Fix AF-0936: Surviving mutant: the lib526-family port can clean up its handles when upstream does not
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary/Lib526.cs]
requirement: none
created: 2026-11-21
completed:
---
# BL-5695 — Fix AF-0936: Surviving mutant: the lib526-family port can clean up its handles when upstream does not

## Goal

The defect the audit office reported as AF-0936 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0936 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib526.cs:147`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib526.cs:147`

Seed 812510 sample of Curl.Conformance.LibTest.UnitLibrary: (testNumber == 527 && steps.Result != CurlCode.Ok) mutated to || survived, so lib527 always cleans up and the other ports clean up after any failure, unlike upstream lib526.c. No test drives this branch both ways.

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/Lib526.cs line 147` as the evidence above describes (the mutant operator is `&&`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/Lib526.cs line 147`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing. Criteria at claim: 4689eb59 9742157d.
