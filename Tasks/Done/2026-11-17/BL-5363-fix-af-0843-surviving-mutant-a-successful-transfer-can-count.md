---
id: BL-5363
title: Fix AF-0843: Surviving mutant: a successful transfer can count as a connection cut off after its head
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs]
requirement: none
created: 2026-11-17
completed: 2026-11-17
---
# BL-5363 — Fix AF-0843: Surviving mutant: a successful transfer can count as a connection cut off after its head

## Goal

The defect the audit office reported as AF-0843 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0843 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:296`

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:296`

result.ExitCode != CurlExitCode.Ok -> == survived in CutOffAfterHead, which feeds CountsAFailedConnection. A transfer with no report and a response code would be counted as a failed connection on success and not counted on failure. That changes the connection count Info reports, and no test notices.

Reproduction: the audit office's mutation test changed `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs line 296` as the evidence above describes (the mutant operator is `!=`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs line 296`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added NumConnectsOf_SuccessfulTransferWithAHeadAndNoReport_IsZero and NumConnectsOf_ReceiveFailedAfterTheHeadWithNoReport_IsOne in ManagedEasyTransferTests.NumConnects.cs; with line 296 mutated to == both fail, reverted all pass. Engine fast tests: 3072 passed, 0 failed. No production code changed.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: 5f2fe122 9742157d.
- 2026-11-17: Doing -> Done. Tests now kill the CutOffAfterHead != mutant
