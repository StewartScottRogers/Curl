---
id: AF-0316
title: ManagedEasyWebSocket.SendFramedAsync's frameLength >= 0 can become > 0 with no test failing, so a CURLWS_OFFSET send with fragment size 0 is sent instead of refused
auditor: quality
severity: High
status: accepted
reason:
key: quality:Curl.Engine.UnitLibrary/ManagedEasyWebSocket.cs:SendFramedAsync-ge:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedEasyWebSocket.cs:231:>=
task: none
tasks:
found: 2026-10-22
found-at: 167e765b2a009a4887fc971075283ef9d24a73a1
scorecard: 2026-10-22_0130.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0316 - ManagedEasyWebSocket.SendFramedAsync's frameLength >= 0 can become > 0 with no test failing, so a CURLWS_OFFSET send with fragment size 0 is sent instead of refused

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/ManagedEasyWebSocket.cs:231`: ManagedEasyWebSocket.SendFramedAsync's frameLength >= 0 can become > 0 with no test failing, so a CURLWS_OFFSET send with fragment size 0 is sent instead of refused.

## Evidence

Location: `Curl.Engine.UnitLibrary/ManagedEasyWebSocket.cs:231`

Sampled mutant (seed 0) survived: 'if (frameLength >= 0 && buffer.Length > frameLength) return new CurlWebSocketSendResult(CurlCode.BadFunctionArgument, 0);' with >= turned into >. frameLength is fragmentSize under CurlWebSocketFlags.Offset. With fragmentSize 0 and a non-empty buffer, the original refuses with BadFunctionArgument, as lib/ws.c does. The mutant skips the check and writes the frame, so a refused input reaches the server and the API caller gets the wrong CurlCode.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedEasyWebSocket.cs:231:>= -Member SendFramedAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
