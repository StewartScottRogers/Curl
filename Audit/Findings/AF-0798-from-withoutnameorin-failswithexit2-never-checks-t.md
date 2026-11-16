---
id: AF-0798
title: From_WithoutNameOrIn_FailsWithExit2 never checks the exit code 2 its name promises
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-16_0514.md: the reproduction no longer reproduces.
key: quality:Curl.Extension.OpenApi.UnitTests/OpenApiParameterTests.cs:From_WithoutNameOrIn_FailsWithExit2:name-lies
reproduction: none
task: BL-5249
tasks: BL-5249
found: 2026-11-15
found-at: 2a5b0f025b7c8fef02cb85fb555414235eb4e132
scorecard: 2026-11-15_1610.md
duplicate-of:
closed: 2026-11-16
closed-how: reliable-reaudit
closed-by: 2026-11-16_0514.md
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

- 2026-11-16 | 2026-11-16_0514.md | reproduces: no | Ran the Select-String reproduction. From_WithoutNameOrIn_FailsWithExit2 now asserts `Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode);` (line 40), and CurlExitCode.FailedInit = 2 (Curl.Protocol.Abstractions.UnitLibrary/CurlExitCode.cs:30).

## Log

- 2026-11-15: filed proposed.
- 2026-11-16: accepted -> closed. Re-audit 2026-11-16_0514.md: the reproduction no longer reproduces.
