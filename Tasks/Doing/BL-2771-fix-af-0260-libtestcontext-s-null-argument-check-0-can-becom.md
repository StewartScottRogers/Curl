---
id: BL-2771
title: Fix AF-0260: LibTestContext's null-argument check >= 0 can become > 0 with no test failing, accepting a null first argument
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests]
requirement: none
created: 2026-10-18
completed:
---
# BL-2771 — Fix AF-0260: LibTestContext's null-argument check >= 0 can become > 0 with no test failing, accepting a null first argument

## Goal

The defect the audit office reported as AF-0260 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0260 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0260-libtestcontext-s-null-argument-check-0-can-become.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/LibTestContext.cs:25`

Location: `Curl.Conformance.LibTest.UnitLibrary/LibTestContext.cs:25`

LibTest sample, seed 0: survived. Original `if (Array.IndexOf(copy, null) >= 0) throw new ArgumentNullException(...)`, mutated `> 0`. A null at index 0, which is the URL every port reads, is no longer refused. No test passes a null first argument.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/LibTestContext.cs:25:>= -Member LibTestContext -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added Curl.Conformance.LibTest.UnitTests to touches: the killing test lives in the library's own test project; no task in Doing names it.
- Fix is test-only: Constructor_RefusesANullFirstArgument passes a null at index 0, so the `>= 0` -> `> 0` mutant fails it. Production code unchanged.
- The audit guard refuses lanes the finding's mutation script, so the reproduction was checked by hand: with `>= 0` changed to `> 0`, LibTestContextTests failed 1 of 13 (killed); restored, 13 of 13 pass and the full fast run is green.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
