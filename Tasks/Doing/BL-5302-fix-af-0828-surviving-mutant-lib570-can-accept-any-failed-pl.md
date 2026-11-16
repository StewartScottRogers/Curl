---
id: BL-5302
title: Fix AF-0828: Surviving mutant: lib570 can accept any failed PLAY as the expected session-ID mismatch
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary/Lib570.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5302 — Fix AF-0828: Surviving mutant: lib570 can accept any failed PLAY as the expected session-ID mismatch

## Goal

The defect the audit office reported as AF-0828 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0828 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib570.cs:94`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib570.cs:94`

Mutation sample (seed 413266): '&& steps.Result == CurlCode.RtspSessionError)' mutated to '|| steps.Result == CurlCode.RtspSessionError)' survived. With the mutant, a PLAY that fails with any code (couldn't connect, recv error) resets the result to Ok and returns, so lib570 reports success where upstream's lib570 fails with 'Failed to detect a Session ID mismatch'. That changes the port's exit code and the upstream case's verdict.

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/Lib570.cs line 94` as the evidence above describes (the mutant operator is `&&`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/Lib570.cs line 94`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: 9bc0c09a 9742157d.
