---
id: AF-0788
title: ArgumentsForValue_WithoutCompanions_AsksForTheOperation never checks the message that asks for the operation
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Extension.OpenApi.UnitTests/OpenApiArgumentExpanderTests.cs:ArgumentsForValue_WithoutCompanions_AsksForTheOperation:name-lies
reproduction: none
task: none
tasks:
found: 2026-11-15
found-at: 065474f7431e6647cda727e7f0fd5a25c9859a90
scorecard: 2026-11-15_1159.md
duplicate-of:
closed:
closed-how:
closed-by:
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

## Log

- 2026-11-15: filed proposed.
