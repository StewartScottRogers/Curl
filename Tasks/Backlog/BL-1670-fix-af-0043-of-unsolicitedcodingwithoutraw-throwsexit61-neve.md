---
id: BL-1670
title: Fix AF-0043: Of_UnsolicitedCodingWithoutRaw_ThrowsExit61 never checks exit code 61
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1670 — Fix AF-0043: Of_UnsolicitedCodingWithoutRaw_ThrowsExit61 never checks exit code 61

## Goal

The defect the audit office reported as AF-0043 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0043 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0043-of-unsolicitedcodingwithoutraw-throwsexit61-never.md`.

Location: `Curl.Protocol.Http.UnitTests/HttpResponseBodyFramingTests.cs:50`

Location: `Curl.Protocol.Http.UnitTests/HttpResponseBodyFramingTests.cs:50`

Step 0 scan candidate (name-lies), read in full. The whole body is 'Assert.ThrowsExactly<HttpTransferException>(() => HttpResponseBodyFraming.Of(Headers("gzip", null), passesTransferCoding: false, ignoresContentLength: true));'. It discards the exception and never checks ExitCode or CurlExitCode.BadContentEncoding. The test just above it (Of_InvalidContentLength_ThrowsExit8, line 41) does check its exit code. A refusal that threw HttpTransferException with any other exit code, for example 8 or 56, would pass, although that is the exit code scripts see.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Protocol.Http.UnitTests/HttpResponseBodyFramingTests.cs -Pattern 'Of_UnsolicitedCodingWithoutRaw_ThrowsExit61' -Context 0,3
```

- Expected: The body captures the exception and asserts Assert.AreEqual(CurlExitCode.BadContentEncoding, thrown.ExitCode).
- Actual: Lines 50-52: an expression-bodied Assert.ThrowsExactly<HttpTransferException>(...) with no exit-code assertion.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
