---
id: AF-0578
title: Arguments_UnsupportedMediaType_FailsWithExit2 never checks exit 2
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extension.OpenApi.UnitTests/OpenApiRequestBodyTests.cs:Arguments_UnsupportedMediaType_FailsWithExit2:name-lies
reproduction: none
task: BL-4742
tasks: BL-4742
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0578 - Arguments_UnsupportedMediaType_FailsWithExit2 never checks exit 2

## Summary

High finding from the quality auditor at `Curl.Extension.OpenApi.UnitTests/OpenApiRequestBodyTests.cs:67`: Arguments_UnsupportedMediaType_FailsWithExit2 never checks exit 2. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Extension.OpenApi.UnitTests/OpenApiRequestBodyTests.cs:67`

Step 0 name-lies. The body asserts only the ArgumentExpansionException's Message; ExitCode is never compared with FailedInit (2).

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.OpenApi.UnitTests/OpenApiRequestBodyTests.cs -Pattern 'Arguments_UnsupportedMediaType_FailsWithExit2' -Context 0,4
```

- Expected: The body asserts the exception's ExitCode is FailedInit.
- Actual: Only the message is asserted.

## Re-audits

- 2026-11-03 | 2026-11-03_1615.md | reproduces: no | Asserts the message and Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode).

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
