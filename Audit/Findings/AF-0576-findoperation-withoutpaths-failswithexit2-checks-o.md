---
id: AF-0576
title: FindOperation_WithoutPaths_FailsWithExit2 checks only that an ArgumentExpansionException is thrown
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Extension.OpenApi.UnitTests/OpenApiDocumentTests.cs:FindOperation_WithoutPaths_FailsWithExit2:name-lies
reproduction: none
task: none
tasks:
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0576 - FindOperation_WithoutPaths_FailsWithExit2 checks only that an ArgumentExpansionException is thrown

## Summary

High finding from the quality auditor at `Curl.Extension.OpenApi.UnitTests/OpenApiDocumentTests.cs:86`: FindOperation_WithoutPaths_FailsWithExit2 checks only that an ArgumentExpansionException is thrown. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Extension.OpenApi.UnitTests/OpenApiDocumentTests.cs:86`

Step 0 name-lies. The whole body is Assert.ThrowsExactly<ArgumentExpansionException>(() => Document(...).FindOperation("a")); neither exit 2 nor the message is checked.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.OpenApi.UnitTests/OpenApiDocumentTests.cs -Pattern 'FindOperation_WithoutPaths_FailsWithExit2' -Context 0,2
```

- Expected: The body asserts the exception's ExitCode is FailedInit.
- Actual: Only Assert.ThrowsExactly<ArgumentExpansionException>(...).

## Re-audits

## Log

- 2026-11-03: filed proposed.
