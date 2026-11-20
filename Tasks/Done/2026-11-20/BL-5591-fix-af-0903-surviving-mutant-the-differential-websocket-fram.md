---
id: BL-5591
title: Fix AF-0903: Surviving mutant: the differential WebSocket frame builder can encode a 126-byte payload in the short form
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Differential.UnitLibrary/Libcurl/WebSocket/LibcurlWebSocketFrames.cs]
requirement: none
created: 2026-11-19
completed: 2026-11-19
---
# BL-5591 — Fix AF-0903: Surviving mutant: the differential WebSocket frame builder can encode a 126-byte payload in the short form

## Goal

The defect the audit office reported as AF-0903 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0903 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.Differential.UnitLibrary/Libcurl/WebSocket/LibcurlWebSocketFrames.cs:35`

Location: `Curl.Conformance.Differential.UnitLibrary/Libcurl/WebSocket/LibcurlWebSocketFrames.cs:35`

Mutant payload.Length < ShortLengthLimit -> <= survived. RFC 6455 reserves length 126 as the 16-bit extended-length marker. Mutated, a 126-byte payload is written with length byte 126 and no extended length, which is a malformed frame sent to both libcurls. No test builds a frame at the 126-byte boundary.

Reproduction: the finding's reproduction runs an audit office tool, which a lane may not run; the audit office reruns it on the re-audit. Prove the fix with a test instead.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] A fast test fails on the code as the evidence above describes it and passes after the fix; Notes name the test.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Added `Frame_PayloadOfExactly126Bytes_WritesTheExtendedLengthMarkerAndLength` (and `Frame_PayloadOf125Bytes_WritesOneLengthByte`) in LibcurlWebSocketFramesTests. With `<` mutated to `<=`, the 126-byte test fails (frame would be 0x82,126,payload with length 128 bytes, not 130). Production code was already correct, so no change there. Build and Differential fast tests green (623).

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing. Criteria at claim: cce9bcd4 9742157d.
- 2026-11-19: Doing -> Done. 126-byte boundary test added for the differential WebSocket frame builder
