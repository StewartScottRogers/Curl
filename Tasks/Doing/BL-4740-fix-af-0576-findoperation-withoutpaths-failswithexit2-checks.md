---
id: BL-4740
title: Fix AF-0576: FindOperation_WithoutPaths_FailsWithExit2 checks only that an ArgumentExpansionException is thrown
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.OpenApi.UnitTests]
requirement: none
created: 2026-11-03
completed:
---
# BL-4740 — Fix AF-0576: FindOperation_WithoutPaths_FailsWithExit2 checks only that an ArgumentExpansionException is thrown

## Goal

The defect the audit office reported as AF-0576 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0576 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0576-findoperation-withoutpaths-failswithexit2-checks-o.md`.

Location: `Curl.Extension.OpenApi.UnitTests/OpenApiDocumentTests.cs:86`

Location: `Curl.Extension.OpenApi.UnitTests/OpenApiDocumentTests.cs:86`

Step 0 name-lies. The whole body is Assert.ThrowsExactly<ArgumentExpansionException>(() => Document(...).FindOperation("a")); neither exit 2 nor the message is checked.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.OpenApi.UnitTests/OpenApiDocumentTests.cs -Pattern 'FindOperation_WithoutPaths_FailsWithExit2' -Context 0,2
```

- Expected: The body asserts the exception's ExitCode is FailedInit.
- Actual: Only Assert.ThrowsExactly<ArgumentExpansionException>(...).

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
