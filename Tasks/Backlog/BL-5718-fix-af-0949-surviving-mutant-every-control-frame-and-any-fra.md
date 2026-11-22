---
id: BL-5718
title: Fix AF-0949: Surviving mutant: every control frame, and any frame over 125 bytes, can be refused as too large (IsControlPayloadTooLong && -> ||)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.WebSocket.UnitLibrary/WsFrameEncoder.cs]
requirement: none
created: 2026-11-21
completed:
---
# BL-5718 — Fix AF-0949: Surviving mutant: every control frame, and any frame over 125 bytes, can be refused as too large (IsControlPayloadTooLong && -> ||)

## Goal

The defect the audit office reported as AF-0949 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0949 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.WebSocket.UnitLibrary/WsFrameEncoder.cs:43`

Location: `Curl.WebSocket.UnitLibrary/WsFrameEncoder.cs:43`

Line 43: opcode is WsOpcode.Close or WsOpcode.Ping or WsOpcode.Pong && payloadLength > LongestControlPayloadLength; mutated to || survived every test in Curl.WebSocket.UnitTests. Curl.Engine.UnitLibrary/ManagedEasyWebSocket.cs:346 returns CurlCode.TooLarge when it is true, so under the mutant curl_ws_send refuses every ping, pong and close and every text or binary frame longer than 125 bytes: a return code users see. Nothing in the library's own twin calls IsControlPayloadTooLong (all 49 WebSocket.UnitTests read).

Reproduction: the audit office's mutation test changed `Curl.WebSocket.UnitLibrary/WsFrameEncoder.cs line 43` as the evidence above describes (the mutant operator is `&&`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.WebSocket.UnitLibrary/WsFrameEncoder.cs line 43`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-21: Created.
