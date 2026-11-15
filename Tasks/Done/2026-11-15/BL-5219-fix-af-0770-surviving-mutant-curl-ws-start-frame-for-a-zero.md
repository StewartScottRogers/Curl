---
id: BL-5219
title: Fix AF-0770: Surviving mutant: curl_ws_start_frame for a zero-length upload frame can answer SendError
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-14
completed: 2026-11-14
---
# BL-5219 — Fix AF-0770: Surviving mutant: curl_ws_start_frame for a zero-length upload frame can answer SendError

## Goal

The defect the audit office reported as AF-0770 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0770 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/ManagedEasyWebSocket.cs:229`

Location: `Curl.Engine.UnitLibrary/ManagedEasyWebSocket.cs:229`

Mutant 'if (frameLength < 0 || !UploadFrames.CanStartFrame)' -> 'frameLength <= 0' survived. The doc comment says SendError is for a negative length only; a zero-length frame (an empty text frame or a close) is valid. The mutant changes the CurlCode the libcurl API returns for it, and no Engine test starts a zero-length upload frame.

Reproduction: the audit office's mutation test changed `Curl.Engine.UnitLibrary/ManagedEasyWebSocket.cs line 229` as the evidence above describes (the mutant operator is `<`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Engine.UnitLibrary/ManagedEasyWebSocket.cs line 229`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by PerformAsync_ReadCallbackStartsAZeroLengthFrame_IsAccepted (Curl.Engine.UnitTests/ManagedEasyTransferTests.WebSocket.cs); fails with the mutant, passes reverted. Test-only change, no production edit.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing. Criteria at claim: 69b90106 9742157d.
- 2026-11-14: Doing -> Done. Zero-length upload frame test kills the mutant
