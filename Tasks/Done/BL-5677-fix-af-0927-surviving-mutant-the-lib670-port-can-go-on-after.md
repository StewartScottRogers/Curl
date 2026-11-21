---
id: BL-5677
title: Fix AF-0927: Surviving mutant: the lib670 port can go on after naming its mime part fails
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary/Lib670.cs]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-5677 — Fix AF-0927: Surviving mutant: the lib670 port can go on after naming its mime part fails

## Goal

The defect the audit office reported as AF-0927 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0927 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib670.cs:102`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib670.cs:102`

Mutant 'return false;' -> 'return true;' after 'if (steps.Result != CurlCode.Ok)' on part.SetName("field") survived (seed 276683 sample). The port writes 'Something went wrong when building the mime structure' and then carries on to bind and perform instead of stopping, as upstream lib670 does. No test makes SetName fail.

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/Lib670.cs line 102` as the evidence above describes (the mutant operator is `false`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/Lib670.cs line 102`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by `Lib670_WhenNamingItsMimePartFails_WritesTheErrorAndDoesNotTransfer` (Lib666To695Tests.cs); mutant applied by hand failed it, reverted, 1234 fast tests pass. Production code unchanged.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing. Criteria at claim: 160170d2 9742157d.
- 2026-11-21: Doing -> Done. Test Lib670_WhenNamingItsMimePartFails kills the mutant.
