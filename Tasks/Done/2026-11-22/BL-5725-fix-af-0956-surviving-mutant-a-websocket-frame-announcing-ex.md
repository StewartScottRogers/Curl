---
id: BL-5725
title: Fix AF-0956: Surviving mutant: a WebSocket frame announcing exactly 2^63-1 bytes can be refused as longer than 63 bits
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.WebSocket.UnitLibrary/WsFrameDecoder.cs]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-5725 — Fix AF-0956: Surviving mutant: a WebSocket frame announcing exactly 2^63-1 bytes can be refused as longer than 63 bits

## Goal

The defect the audit office reported as AF-0956 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0956 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.WebSocket.UnitLibrary/WsFrameDecoder.cs:261`

Location: `Curl.WebSocket.UnitLibrary/WsFrameDecoder.cs:261`

Line 261: if (length > long.MaxValue) throw Violation("frame length longer than 63 bits not supported"); mutated to >= survived. A 64-bit length of exactly long.MaxValue is legal, and the mutant turns it into a protocol violation. Only a 10-byte header is needed to test it, but Decode_64BitLengthWithTheTopBitSet_StopsWithAViolation covers only 0x8000000000000000.

Reproduction: the audit office's mutation test changed `Curl.WebSocket.UnitLibrary/WsFrameDecoder.cs line 261` as the evidence above describes (the mutant operator is `>`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.WebSocket.UnitLibrary/WsFrameDecoder.cs line 261`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by new test `Decode_64BitLengthOfExactlyLongMaxValue_IsAcceptedNotAViolation` (WsFrameDecoderTests): with `>=` applied by hand it fails, reverted all 80 pass. Test-only change; WsFrameDecoder.cs untouched.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing. Criteria at claim: ff4f9e80 9742157d.
- 2026-11-21: Doing -> Done. Mutant killed by new test
