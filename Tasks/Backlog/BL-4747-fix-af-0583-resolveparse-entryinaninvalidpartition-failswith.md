---
id: BL-4747
title: Fix AF-0583: ResolveParse_EntryInAnInvalidPartition_FailsWithCurlsExit49Text never checks exit 49
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-11-03
completed:
---
# BL-4747 — Fix AF-0583: ResolveParse_EntryInAnInvalidPartition_FailsWithCurlsExit49Text never checks exit 49

## Goal

The defect the audit office reported as AF-0583 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0583 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0583-resolveparse-entryinaninvalidpartition-failswithcu.md`.

Location: `Curl.Networking.UnitTests/NetworkingAdversarialTests.cs:460`

Location: `Curl.Networking.UnitTests/NetworkingAdversarialTests.cs:460`

Step 0 name-lies. ResolveOverrides exposes the exit code (ParseFailureCodeWith, ResolveOverrides.cs:71, SetoptOptionSyntax = 49) but the test asserts only overrides.ParseError's text and the entry count.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/NetworkingAdversarialTests.cs -Pattern 'ResolveParse_EntryInAnInvalidPartition_FailsWithCurlsExit49Text' -Context 0,7
```

- Expected: The body asserts CurlExitCode.SetoptOptionSyntax.
- Actual: Only ParseError, Entries and Find are asserted.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
