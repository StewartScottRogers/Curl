---
id: BL-5239
title: Fix AF-0788: ArgumentsForValue_WithoutCompanions_AsksForTheOperation never checks the message that asks for the operation
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.OpenApi.UnitTests]
requirement: none
created: 2026-11-15
completed:
---
# BL-5239 — Fix AF-0788: ArgumentsForValue_WithoutCompanions_AsksForTheOperation never checks the message that asks for the operation

## Goal

The defect the audit office reported as AF-0788 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0788 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extension.OpenApi.UnitTests/OpenApiArgumentExpanderTests.cs:123`

Location: `Curl.Extension.OpenApi.UnitTests/OpenApiArgumentExpanderTests.cs:123`

Step 1. The body is only Assert.ThrowsExactly<ArgumentExpansionException>(() => ...ArgumentsFor("pets.json", ...)). The name promises that the refusal asks for the operation, which is OpenApiArgumentExpander.cs:39's message "--operation <id> names the operation to call". Any ArgumentExpansionException passes, including a read error or the 'one --operation per --openapi' refusal. The sibling tests ArgumentsFor_WithoutOperation_FailsWithExit2 and ArgumentsFor_TwoOperations_FailsWithExit2 assert both the message and the exit code.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.OpenApi.UnitTests/OpenApiArgumentExpanderTests.cs -Pattern 'AsksForTheOperation' -Context 0,2
```

- Expected: The test asserts the failure's Message equals "openapi: --operation <id> names the operation to call".
- Actual: Only Assert.ThrowsExactly<ArgumentExpansionException>(...); no message is checked.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
