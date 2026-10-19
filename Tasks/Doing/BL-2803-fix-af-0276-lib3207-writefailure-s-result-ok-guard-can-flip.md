---
id: BL-2803
title: Fix AF-0276: Lib3207.WriteFailure's result == Ok guard can flip with no test failing, so failures go unreported and successes are reported as failures
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-10-19
completed:
---
# BL-2803 — Fix AF-0276: Lib3207.WriteFailure's result == Ok guard can flip with no test failing, so failures go unreported and successes are reported as failures

## Goal

The defect the audit office reported as AF-0276 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0276 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0276-lib3207-writefailure-s-result-ok-guard-can-flip-wi.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib3207.cs:137`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib3207.cs:137`

Mutant if (result == CurlCode.Ok) return; -> != survived (sampled run, seed 0). The thread's failure line on stderr is then written for successful transfers and never for failed ones. That is output bytes of the port, and no test pins them.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib3207.cs:137:== -Member WriteFailure -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
