---
id: AF-0574
title: ArgumentsFor_TwoOperations_FailsWithExit2 never checks exit 2
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extension.OpenApi.UnitTests/OpenApiArgumentExpanderTests.cs:ArgumentsFor_TwoOperations_FailsWithExit2:name-lies
reproduction: none
task: BL-4738
tasks: BL-4738
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0574 - ArgumentsFor_TwoOperations_FailsWithExit2 never checks exit 2

## Summary

High finding from the quality auditor at `Curl.Extension.OpenApi.UnitTests/OpenApiArgumentExpanderTests.cs:103`: ArgumentsFor_TwoOperations_FailsWithExit2 never checks exit 2. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Extension.OpenApi.UnitTests/OpenApiArgumentExpanderTests.cs:103`

Step 0 name-lies. ArgumentExpansionException carries ExitCode (Curl.Extension.Abstractions.UnitLibrary/Arguments/ArgumentExpansionException.cs:16) but the test asserts only its type and Message; an expansion failing with another exit code passes.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.OpenApi.UnitTests/OpenApiArgumentExpanderTests.cs -Pattern 'ArgumentsFor_TwoOperations_FailsWithExit2' -Context 0,7
```

- Expected: The body asserts Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode).
- Actual: The body asserts only failure.Message.

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
