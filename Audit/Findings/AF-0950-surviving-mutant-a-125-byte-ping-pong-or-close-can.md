---
id: AF-0950
title: Surviving mutant: a 125-byte ping, pong or close can be refused as too large (payloadLength > 125 -> >= 125)
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.WebSocket.UnitLibrary/WsFrameEncoder.cs:IsControlPayloadTooLong-gt:surviving-mutant
reproduction: mutation Curl.WebSocket.UnitLibrary/WsFrameEncoder.cs:43:>
task: BL-5719
tasks: BL-5719
found: 2026-11-21
found-at: 8d41979abc77036a1cf5ff183027379b16dc1eea
scorecard: 2026-11-21_1740.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0950 - Surviving mutant: a 125-byte ping, pong or close can be refused as too large (payloadLength > 125 -> >= 125)

## Summary

High finding from the quality auditor at `Curl.WebSocket.UnitLibrary/WsFrameEncoder.cs:43`: Surviving mutant: a 125-byte ping, pong or close can be refused as too large (payloadLength > 125 -> >= 125). Reported by an auditor flagged unreliable in 2026-11-21_1740.md.

## Evidence

Location: `Curl.WebSocket.UnitLibrary/WsFrameEncoder.cs:43`

Same line 43, the > mutated to >= survived. RFC 6455 section 5.5 allows a control payload of exactly 125 bytes (LongestControlPayloadLength = 125, line 32); under the mutant curl_ws_send (ManagedEasyWebSocket.cs:346) answers CURLE_TOO_LARGE for it. No test in the twin sends a control frame of exactly 125 bytes through this check (Encode_125BytePayload_Uses7BitLength encodes one but never asks IsControlPayloadTooLong).

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.WebSocket.UnitLibrary/WsFrameEncoder.cs:43:> -Member IsControlPayloadTooLong -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-21: filed proposed.
