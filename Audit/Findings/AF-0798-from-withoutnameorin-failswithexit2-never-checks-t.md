---
id: AF-0798
title: From_WithoutNameOrIn_FailsWithExit2 never checks the exit code 2 its name promises
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Extension.OpenApi.UnitTests/OpenApiParameterTests.cs:From_WithoutNameOrIn_FailsWithExit2:name-lies
reproduction: none
task: none
tasks:
found: 2026-11-15
found-at: 2a5b0f025b7c8fef02cb85fb555414235eb4e132
scorecard: 2026-11-15_1610.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0798 - From_WithoutNameOrIn_FailsWithExit2 never checks the exit code 2 its name promises

## Summary

Medium finding from the quality auditor at `Curl.Extension.OpenApi.UnitTests/OpenApiParameterTests.cs:35`: From_WithoutNameOrIn_FailsWithExit2 never checks the exit code 2 its name promises.

## Evidence

Location: `Curl.Extension.OpenApi.UnitTests/OpenApiParameterTests.cs:35`

Step 1 (OpenApi twin read in full). The whole body is line 36: Assert.AreEqual(message, Assert.ThrowsExactly<ArgumentExpansionException>(() => OpenApiParameter.From(Parse(json))).Message); The exception's ExitCode is never read, so a refusal with any exit code (for example ReadError instead of FailedInit) passes. Its sibling tests that make the same promise check it, for example OpenApiRequestBodyTests Arguments_UnsupportedMediaType_FailsWithExit2 asserts Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode). The scanner did not list this test.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.OpenApi.UnitTests/OpenApiParameterTests.cs -Pattern 'From_WithoutNameOrIn_FailsWithExit2' -Context 0,1
```

- Expected: The body asserts Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode) beside the message.
- Actual: Line 35 the test, line 36 Assert.AreEqual(message, Assert.ThrowsExactly<ArgumentExpansionException>(...).Message); and nothing else.

## Re-audits

## Log

- 2026-11-15: filed proposed.
