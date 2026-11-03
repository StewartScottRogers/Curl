---
id: BL-4738
title: Fix AF-0574: ArgumentsFor_TwoOperations_FailsWithExit2 never checks exit 2
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.OpenApi.UnitTests]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4738 — Fix AF-0574: ArgumentsFor_TwoOperations_FailsWithExit2 never checks exit 2

## Goal

The defect the audit office reported as AF-0574 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0574 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0574-argumentsfor-twooperations-failswithexit2-never-ch.md`.

Location: `Curl.Extension.OpenApi.UnitTests/OpenApiArgumentExpanderTests.cs:103`

Location: `Curl.Extension.OpenApi.UnitTests/OpenApiArgumentExpanderTests.cs:103`

Step 0 name-lies. ArgumentExpansionException carries ExitCode (Curl.Extension.Abstractions.UnitLibrary/Arguments/ArgumentExpansionException.cs:16) but the test asserts only its type and Message; an expansion failing with another exit code passes.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.OpenApi.UnitTests/OpenApiArgumentExpanderTests.cs -Pattern 'ArgumentsFor_TwoOperations_FailsWithExit2' -Context 0,7
```

- Expected: The body asserts Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode).
- Actual: The body asserts only failure.Message.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Added the missing `Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode)` to the test; OpenApi tests 161 pass. Test-only change, no library code.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. Test now asserts exit code 2
