---
id: AF-0575
title: FindOperation_UnknownId_FailsWithExit2 never checks exit 2
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-03_2203.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-11-03_1615.md, 2026-11-03_2203.md).
key: quality:Curl.Extension.OpenApi.UnitTests/OpenApiDocumentTests.cs:FindOperation_UnknownId_FailsWithExit2:name-lies
reproduction: none
task: BL-4739
tasks: BL-4739
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed: 2026-11-03
closed-how: consecutive
closed-by: 2026-11-03_1615.md, 2026-11-03_2203.md
---
# AF-0575 - FindOperation_UnknownId_FailsWithExit2 never checks exit 2

## Summary

High finding from the quality auditor at `Curl.Extension.OpenApi.UnitTests/OpenApiDocumentTests.cs:80`: FindOperation_UnknownId_FailsWithExit2 never checks exit 2. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Extension.OpenApi.UnitTests/OpenApiDocumentTests.cs:80`

Step 0 name-lies. The body asserts only the ArgumentExpansionException's Message; its ExitCode is never compared with CurlExitCode.FailedInit (2).

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.OpenApi.UnitTests/OpenApiDocumentTests.cs -Pattern 'FindOperation_UnknownId_FailsWithExit2' -Context 0,4
```

- Expected: The body asserts the exception's ExitCode is FailedInit.
- Actual: Assert.AreEqual("openapi: no operation nope in the description", Assert.ThrowsExactly<ArgumentExpansionException>(...).Message);

## Re-audits

- 2026-11-03 | 2026-11-03_1615.md | reproduces: no | Asserts Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode) after the message.
- 2026-11-03 | 2026-11-03_2203.md | reproduces: no | Ran the reproduction: the test now ends with Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode) at line 85 (FailedInit = 2).

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_2203.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-11-03_1615.md, 2026-11-03_2203.md).
