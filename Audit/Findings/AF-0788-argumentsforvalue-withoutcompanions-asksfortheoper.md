---
id: AF-0788
title: ArgumentsForValue_WithoutCompanions_AsksForTheOperation never checks the message that asks for the operation
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-15_1610.md: the reproduction no longer reproduces.
key: quality:Curl.Extension.OpenApi.UnitTests/OpenApiArgumentExpanderTests.cs:ArgumentsForValue_WithoutCompanions_AsksForTheOperation:name-lies
reproduction: none
task: BL-5239
tasks: BL-5239
found: 2026-11-15
found-at: 065474f7431e6647cda727e7f0fd5a25c9859a90
scorecard: 2026-11-15_1159.md
duplicate-of:
closed: 2026-11-15
closed-how: reliable-reaudit
closed-by: 2026-11-15_1610.md
---
# AF-0788 - ArgumentsForValue_WithoutCompanions_AsksForTheOperation never checks the message that asks for the operation

## Summary

Medium finding from the quality auditor at `Curl.Extension.OpenApi.UnitTests/OpenApiArgumentExpanderTests.cs:123`: ArgumentsForValue_WithoutCompanions_AsksForTheOperation never checks the message that asks for the operation.

## Evidence

Location: `Curl.Extension.OpenApi.UnitTests/OpenApiArgumentExpanderTests.cs:123`

Step 1. The body is only Assert.ThrowsExactly<ArgumentExpansionException>(() => ...ArgumentsFor("pets.json", ...)). The name promises that the refusal asks for the operation, which is OpenApiArgumentExpander.cs:39's message "--operation <id> names the operation to call". Any ArgumentExpansionException passes, including a read error or the 'one --operation per --openapi' refusal. The sibling tests ArgumentsFor_WithoutOperation_FailsWithExit2 and ArgumentsFor_TwoOperations_FailsWithExit2 assert both the message and the exit code.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.OpenApi.UnitTests/OpenApiArgumentExpanderTests.cs -Pattern 'AsksForTheOperation' -Context 0,2
```

- Expected: The test asserts the failure's Message equals "openapi: --operation <id> names the operation to call".
- Actual: Only Assert.ThrowsExactly<ArgumentExpansionException>(...); no message is checked.

## Re-audits

- 2026-11-15 | 2026-11-15_1610.md | reproduces: no | Ran the Select-String reproduction: it shows the test at OpenApiArgumentExpanderTests.cs:123-125, and lines 126-127 now assert Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode) and Assert.AreEqual("openapi: --operation <id> names the operation to call", failure.Message), the exact message that asks for the operation.

## Log

- 2026-11-15: filed proposed.
- 2026-11-15: accepted -> closed. Re-audit 2026-11-15_1610.md: the reproduction no longer reproduces.
