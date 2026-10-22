---
id: AF-0339
title: ManagedEasyTransfer.ReadCallbackPostLengthOf's PostFieldSize < 0 can become <= 0 with no test failing, so a read-callback post with CURLOPT_POSTFIELDSIZE 0 goes chunked
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs:ReadCallbackPostLengthOf-lt:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs:916:<
task: none
tasks:
found: 2026-10-22
found-at: 608a755586679cf34031a0e5b137f01b9d7fb329
scorecard: 2026-10-22_1120.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0339 - ManagedEasyTransfer.ReadCallbackPostLengthOf's PostFieldSize < 0 can become <= 0 with no test failing, so a read-callback post with CURLOPT_POSTFIELDSIZE 0 goes chunked

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs:916`: ManagedEasyTransfer.ReadCallbackPostLengthOf's PostFieldSize < 0 can become <= 0 with no test failing, so a read-callback post with CURLOPT_POSTFIELDSIZE 0 goes chunked.

## Evidence

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs:916`

Sampled mutant (seed 0) survived: 'http.PostFieldSize < 0 || AsksForChunked(...) ? null : http.PostFieldSize' became '<= 0'. A CURLOPT_POST with a read callback and CURLOPT_POSTFIELDSIZE 0 should send Content-Length: 0, as libcurl 8.22.0 does. With the mutant it sends Transfer-Encoding: chunked. The request bytes change and no test noticed. The existing test PerformAsync_PostWithNoPostFieldsAPostFieldSizeOfZeroAndNoReadCallback_... covers size 0 only without a read callback.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs:916:< -Member ReadCallbackPostLengthOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-10-22: filed proposed.
