---
id: BL-5719
title: Fix AF-0950: Surviving mutant: a 125-byte ping, pong or close can be refused as too large (payloadLength > 125 -> >= 125)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.WebSocket.UnitLibrary/WsFrameEncoder.cs]
requirement: none
created: 2026-11-21
completed:
---
# BL-5719 — Fix AF-0950: Surviving mutant: a 125-byte ping, pong or close can be refused as too large (payloadLength > 125 -> >= 125)

## Goal

The defect the audit office reported as AF-0950 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0950 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.WebSocket.UnitLibrary/WsFrameEncoder.cs:43`

Location: `Curl.WebSocket.UnitLibrary/WsFrameEncoder.cs:43`

Same line 43, the > mutated to >= survived. RFC 6455 section 5.5 allows a control payload of exactly 125 bytes (LongestControlPayloadLength = 125, line 32); under the mutant curl_ws_send (ManagedEasyWebSocket.cs:346) answers CURLE_TOO_LARGE for it. No test in the twin sends a control frame of exactly 125 bytes through this check (Encode_125BytePayload_Uses7BitLength encodes one but never asks IsControlPayloadTooLong).

Reproduction: the audit office's mutation test changed `Curl.WebSocket.UnitLibrary/WsFrameEncoder.cs line 43` as the evidence above describes (the mutant operator is `>`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.WebSocket.UnitLibrary/WsFrameEncoder.cs line 43`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-21: Created.
