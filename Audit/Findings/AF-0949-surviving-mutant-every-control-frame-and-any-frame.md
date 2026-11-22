---
id: AF-0949
title: Surviving mutant: every control frame, and any frame over 125 bytes, can be refused as too large (IsControlPayloadTooLong && -> ||)
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.WebSocket.UnitLibrary/WsFrameEncoder.cs:IsControlPayloadTooLong-and:surviving-mutant
reproduction: mutation Curl.WebSocket.UnitLibrary/WsFrameEncoder.cs:43:&&
task: none
tasks:
found: 2026-11-21
found-at: 8d41979abc77036a1cf5ff183027379b16dc1eea
scorecard: 2026-11-21_1740.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0949 - Surviving mutant: every control frame, and any frame over 125 bytes, can be refused as too large (IsControlPayloadTooLong && -> ||)

## Summary

High finding from the quality auditor at `Curl.WebSocket.UnitLibrary/WsFrameEncoder.cs:43`: Surviving mutant: every control frame, and any frame over 125 bytes, can be refused as too large (IsControlPayloadTooLong && -> ||). Reported by an auditor flagged unreliable in 2026-11-21_1740.md.

## Evidence

Location: `Curl.WebSocket.UnitLibrary/WsFrameEncoder.cs:43`

Line 43: opcode is WsOpcode.Close or WsOpcode.Ping or WsOpcode.Pong && payloadLength > LongestControlPayloadLength; mutated to || survived every test in Curl.WebSocket.UnitTests. Curl.Engine.UnitLibrary/ManagedEasyWebSocket.cs:346 returns CurlCode.TooLarge when it is true, so under the mutant curl_ws_send refuses every ping, pong and close and every text or binary frame longer than 125 bytes: a return code users see. Nothing in the library's own twin calls IsControlPayloadTooLong (all 49 WebSocket.UnitTests read).

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.WebSocket.UnitLibrary/WsFrameEncoder.cs:43:&& -Member IsControlPayloadTooLong -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-21: filed proposed.
