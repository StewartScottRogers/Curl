---
id: AF-0956
title: Surviving mutant: a WebSocket frame announcing exactly 2^63-1 bytes can be refused as longer than 63 bits
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.WebSocket.UnitLibrary/WsFrameDecoder.cs:PayloadLength-gt:surviving-mutant
reproduction: mutation Curl.WebSocket.UnitLibrary/WsFrameDecoder.cs:261:>
task: BL-5725
tasks: BL-5725
found: 2026-11-21
found-at: 8d41979abc77036a1cf5ff183027379b16dc1eea
scorecard: 2026-11-21_1740.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0956 - Surviving mutant: a WebSocket frame announcing exactly 2^63-1 bytes can be refused as longer than 63 bits

## Summary

Medium finding from the quality auditor at `Curl.WebSocket.UnitLibrary/WsFrameDecoder.cs:261`: Surviving mutant: a WebSocket frame announcing exactly 2^63-1 bytes can be refused as longer than 63 bits. Reported by an auditor flagged unreliable in 2026-11-21_1740.md.

## Evidence

Location: `Curl.WebSocket.UnitLibrary/WsFrameDecoder.cs:261`

Line 261: if (length > long.MaxValue) throw Violation("frame length longer than 63 bits not supported"); mutated to >= survived. A 64-bit length of exactly long.MaxValue is legal, and the mutant turns it into a protocol violation. Only a 10-byte header is needed to test it, but Decode_64BitLengthWithTheTopBitSet_StopsWithAViolation covers only 0x8000000000000000.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.WebSocket.UnitLibrary/WsFrameDecoder.cs:261:> -Member PayloadLength -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-21: filed proposed.
