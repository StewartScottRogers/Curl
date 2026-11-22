---
id: AF-0949
title: Surviving mutant: every control frame, and any frame over 125 bytes, can be refused as too large (IsControlPayloadTooLong && -> ||)
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-22_0210.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.WebSocket.UnitLibrary/WsFrameEncoder.cs:IsControlPayloadTooLong-and:surviving-mutant
reproduction: mutation Curl.WebSocket.UnitLibrary/WsFrameEncoder.cs:43:&&
task: BL-5718
tasks: BL-5718
found: 2026-11-21
found-at: 8d41979abc77036a1cf5ff183027379b16dc1eea
scorecard: 2026-11-21_1740.md
duplicate-of:
closed: 2026-11-22
closed-how: mechanical
closed-by: 2026-11-22_0210.md
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

- 2026-11-22 | 2026-11-22_0210.md | reproduces: no | Ran the -Site command (&&): resolvedLine 43, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-21: filed proposed.
- 2026-11-22: accepted -> closed. Re-audit 2026-11-22_0210.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
