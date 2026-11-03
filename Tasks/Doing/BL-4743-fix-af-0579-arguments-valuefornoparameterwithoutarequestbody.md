---
id: BL-4743
title: Fix AF-0579: Arguments_ValueForNoParameterWithoutARequestBody_FailsWithExit2 never checks exit 2
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.OpenApi.UnitTests]
requirement: none
created: 2026-11-03
completed:
---
# BL-4743 — Fix AF-0579: Arguments_ValueForNoParameterWithoutARequestBody_FailsWithExit2 never checks exit 2

## Goal

The defect the audit office reported as AF-0579 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0579 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0579-arguments-valuefornoparameterwithoutarequestbody-f.md`.

Location: `Curl.Extension.OpenApi.UnitTests/OpenApiRequestTests.cs:54`

Location: `Curl.Extension.OpenApi.UnitTests/OpenApiRequestTests.cs:54`

Step 0 name-lies. The body asserts only the ArgumentExpansionException's Message; ExitCode is never compared with FailedInit (2).

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.OpenApi.UnitTests/OpenApiRequestTests.cs -Pattern 'Arguments_ValueForNoParameterWithoutARequestBody_FailsWithExit2' -Context 0,4
```

- Expected: The body asserts the exception's ExitCode is FailedInit.
- Actual: Only the message is asserted.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
