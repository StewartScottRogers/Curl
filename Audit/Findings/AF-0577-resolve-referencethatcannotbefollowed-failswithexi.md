---
id: AF-0577
title: Resolve_ReferenceThatCannotBeFollowed_FailsWithExit2 never checks exit 2
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Extension.OpenApi.UnitTests/OpenApiDocumentTests.cs:Resolve_ReferenceThatCannotBeFollowed_FailsWithExit2:name-lies
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
# AF-0577 - Resolve_ReferenceThatCannotBeFollowed_FailsWithExit2 never checks exit 2

## Summary

High finding from the quality auditor at `Curl.Extension.OpenApi.UnitTests/OpenApiDocumentTests.cs:181`: Resolve_ReferenceThatCannotBeFollowed_FailsWithExit2 never checks exit 2. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Extension.OpenApi.UnitTests/OpenApiDocumentTests.cs:181`

Step 0 name-lies. The body asserts only Assert.AreEqual(message, Assert.ThrowsExactly<ArgumentExpansionException>(...).Message); ExitCode is never checked.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.OpenApi.UnitTests/OpenApiDocumentTests.cs -Pattern 'Resolve_ReferenceThatCannotBeFollowed_FailsWithExit2' -Context 0,5
```

- Expected: The body asserts the exception's ExitCode is FailedInit.
- Actual: Only the message is asserted.

## Re-audits

## Log

- 2026-11-03: filed proposed.
