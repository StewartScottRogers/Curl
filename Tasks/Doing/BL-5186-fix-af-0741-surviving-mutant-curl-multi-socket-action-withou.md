---
id: BL-5186
title: Fix AF-0741: Surviving mutant: curl_multi_socket_action without a socket callback can answer CURLM_ABORTED_BY_CALLBACK
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-14
completed:
---
# BL-5186 — Fix AF-0741: Surviving mutant: curl_multi_socket_action without a socket callback can answer CURLM_ABORTED_BY_CALLBACK

## Goal

The defect the audit office reported as AF-0741 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0741 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/ManagedMultiTransfer.MovingOn.cs:153`

Location: `Curl.Engine.UnitLibrary/ManagedMultiTransfer.MovingOn.cs:153`

Mutant return true; -> return false; in UpdateSockets (the branch where Options.Socket is null) survived (seed 641133). Its caller (line 66) is return !socketAction || UpdateSockets() ? UpdateTimer(timerFired) : CurlMultiCode.AbortedByCallback;, so a libcurl user driving the multi handle with socket_action and no CURLMOPT_SOCKETFUNCTION would get AbortedByCallback instead of Ok. No test calls SocketActionAsync on a multi without a Socket callback.

Reproduction: the audit office's mutation test changed `Curl.Engine.UnitLibrary/ManagedMultiTransfer.MovingOn.cs line 153` as the evidence above describes (the mutant operator is `true`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Engine.UnitLibrary/ManagedMultiTransfer.MovingOn.cs line 153`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing. Criteria at claim: 999c072d 9742157d.
