---
id: BL-5704
title: Fix AF-0945: Surviving mutant: the PKCE redirect receiver can keep reading after the request head ends
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.OAuth2.UnitLibrary/Flows/OAuth2RedirectReceiver.cs]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-5704 — Fix AF-0945: Surviving mutant: the PKCE redirect receiver can keep reading after the request head ends

## Goal

The defect the audit office reported as AF-0945 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0945 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extension.OAuth2.UnitLibrary/Flows/OAuth2RedirectReceiver.cs:45`

Location: `Curl.Extension.OAuth2.UnitLibrary/Flows/OAuth2RedirectReceiver.cs:45`

Drawn library, seed 812510: while (filled < buffer.Length && ...IndexOf(CRLFCRLF) < 0) mutated to || survived. The mutant keeps reading after the blank line until the buffer is full; a browser waiting for the answer on an open connection leaves the flow hanging until its timeout. The tests' scripted connections end the stream, so the extra read returns 0.

Reproduction: the audit office's mutation test changed `Curl.Extension.OAuth2.UnitLibrary/Flows/OAuth2RedirectReceiver.cs line 45` as the evidence above describes (the mutant operator is `&&`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Extension.OAuth2.UnitLibrary/Flows/OAuth2RedirectReceiver.cs line 45`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by `OAuth2RedirectReceiverTests.ReceiveAsync_HeadEndedAndBrowserWaitsForAnswer_AnswersWithoutReadingAgain` (the browser connection throws on a second read). Verified by hand: with `||` it fails, reverted all 186 pass. Test-only change; production code unchanged.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing. Criteria at claim: 90c75c03 9742157d.
- 2026-11-21: Doing -> Done. Test pins the mutant
