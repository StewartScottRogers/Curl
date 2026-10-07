---
id: AF-0043
title: Of_UnsolicitedCodingWithoutRaw_ThrowsExit61 never checks exit code 61
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Protocol.Http.UnitTests/HttpResponseBodyFramingTests.cs:Of_UnsolicitedCodingWithoutRaw_ThrowsExit61:name-lies
reproduction: none
task: none
tasks:
found: 2026-10-07
found-at: 5a627a2fb4baf7b4b2662dc309939ec576dcad20
scorecard: 2026-10-07_0844.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0043 - Of_UnsolicitedCodingWithoutRaw_ThrowsExit61 never checks exit code 61

## Summary

Medium finding from the quality auditor at `Curl.Protocol.Http.UnitTests/HttpResponseBodyFramingTests.cs:50`: Of_UnsolicitedCodingWithoutRaw_ThrowsExit61 never checks exit code 61. Reported by an auditor flagged unreliable in 2026-10-07_0844.md.

## Evidence

Location: `Curl.Protocol.Http.UnitTests/HttpResponseBodyFramingTests.cs:50`

Step 0 scan candidate (name-lies), read in full. The whole body is 'Assert.ThrowsExactly<HttpTransferException>(() => HttpResponseBodyFraming.Of(Headers("gzip", null), passesTransferCoding: false, ignoresContentLength: true));'. It discards the exception and never checks ExitCode or CurlExitCode.BadContentEncoding. The test just above it (Of_InvalidContentLength_ThrowsExit8, line 41) does check its exit code. A refusal that threw HttpTransferException with any other exit code, for example 8 or 56, would pass, although that is the exit code scripts see.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Protocol.Http.UnitTests/HttpResponseBodyFramingTests.cs -Pattern 'Of_UnsolicitedCodingWithoutRaw_ThrowsExit61' -Context 0,3
```

- Expected: The body captures the exception and asserts Assert.AreEqual(CurlExitCode.BadContentEncoding, thrown.ExitCode).
- Actual: Lines 50-52: an expression-bodied Assert.ThrowsExactly<HttpTransferException>(...) with no exit-code assertion.

## Re-audits

## Log

- 2026-10-07: filed proposed.
- 2026-10-07: proposed -> accepted.
