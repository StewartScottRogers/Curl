---
id: BL-5247
title: Fix AF-0796: Surviving mutant: any IOException in a read-callback upload can be reported as an aborted-by-callback exit 42
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-15
completed: 2026-11-15
---
# BL-5247 — Fix AF-0796: Surviving mutant: any IOException in a read-callback upload can be reported as an aborted-by-callback exit 42

## Goal

The defect the audit office reported as AF-0796 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0796 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:371`

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:371`

Mutant 'exception is IOException && AnyReadAborted(reads)' -> '||' in FollowUnlessAbortedAsync survived (seed 5851). The predicate decides which escaping exceptions are a read callback's CURL_READFUNC_ABORT (upstream test1507). The mutant treats every IOException (a reset connection, a send failure) and every exception after an abort as AbortedByCallback, so a perform answers 42 'operation aborted by callback' in place of its real code.

Reproduction: the audit office's mutation test changed `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs line 371` as the evidence above describes (the mutant operator is `&&`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs line 371`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Killed by `ManagedEasyTransferFollowUnlessAbortedTests` (`IOExceptionAndNoReadAborted` and `NonIOExceptionAfterAReadAborted`), checked by hand with `&&` -> `||`; both fail with the mutant, all 3048 fast Engine tests pass reverted. `FollowUnlessAbortedAsync` became internal to be tested directly. Measure-CodeQuality for Curl.Engine.UnitLibrary: 0 failing members.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing. Criteria at claim: aeacaa28 9742157d.
- 2026-11-15: Doing -> Done. Mutant killed by new FollowUnlessAborted tests
