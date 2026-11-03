---
id: AF-0579
title: Arguments_ValueForNoParameterWithoutARequestBody_FailsWithExit2 never checks exit 2
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Extension.OpenApi.UnitTests/OpenApiRequestTests.cs:Arguments_ValueForNoParameterWithoutARequestBody_FailsWithExit2:name-lies
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
# AF-0579 - Arguments_ValueForNoParameterWithoutARequestBody_FailsWithExit2 never checks exit 2

## Summary

High finding from the quality auditor at `Curl.Extension.OpenApi.UnitTests/OpenApiRequestTests.cs:54`: Arguments_ValueForNoParameterWithoutARequestBody_FailsWithExit2 never checks exit 2. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Extension.OpenApi.UnitTests/OpenApiRequestTests.cs:54`

Step 0 name-lies. The body asserts only the ArgumentExpansionException's Message; ExitCode is never compared with FailedInit (2).

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.OpenApi.UnitTests/OpenApiRequestTests.cs -Pattern 'Arguments_ValueForNoParameterWithoutARequestBody_FailsWithExit2' -Context 0,4
```

- Expected: The body asserts the exception's ExitCode is FailedInit.
- Actual: Only the message is asserted.

## Re-audits

## Log

- 2026-11-03: filed proposed.
