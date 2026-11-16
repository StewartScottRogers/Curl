---
id: BL-5249
title: Fix AF-0798: From_WithoutNameOrIn_FailsWithExit2 never checks the exit code 2 its name promises
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.OpenApi.UnitTests/OpenApiParameterTests.cs]
requirement: none
created: 2026-11-15
completed:
---
# BL-5249 — Fix AF-0798: From_WithoutNameOrIn_FailsWithExit2 never checks the exit code 2 its name promises

## Goal

The defect the audit office reported as AF-0798 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0798 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extension.OpenApi.UnitTests/OpenApiParameterTests.cs:35`

Location: `Curl.Extension.OpenApi.UnitTests/OpenApiParameterTests.cs:35`

Step 1 (OpenApi twin read in full). The whole body is line 36: Assert.AreEqual(message, Assert.ThrowsExactly<ArgumentExpansionException>(() => OpenApiParameter.From(Parse(json))).Message); The exception's ExitCode is never read, so a refusal with any exit code (for example ReadError instead of FailedInit) passes. Its sibling tests that make the same promise check it, for example OpenApiRequestBodyTests Arguments_UnsupportedMediaType_FailsWithExit2 asserts Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode). The scanner did not list this test.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.OpenApi.UnitTests/OpenApiParameterTests.cs -Pattern 'From_WithoutNameOrIn_FailsWithExit2' -Context 0,1
```

- Expected: The body asserts Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode) beside the message.
- Actual: Line 35 the test, line 36 Assert.AreEqual(message, Assert.ThrowsExactly<ArgumentExpansionException>(...).Message); and nothing else.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-15: Created.
