---
id: BL-4759
title: Fix AF-0595: Surviving mutant: the NSEC3 closest-encloser search can start above the name's own label count without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Dane.UnitLibrary]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4759 — Fix AF-0595: Surviving mutant: the NSEC3 closest-encloser search can start above the name's own label count without a test failing

## Goal

The defect the audit office reported as AF-0595 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0595 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0595-surviving-mutant-the-nsec3-closest-encloser-search.md`.

Location: `Curl.Extension.Dane.UnitLibrary/Lookup/DenialOfExistence.cs:116`

Location: `Curl.Extension.Dane.UnitLibrary/Lookup/DenialOfExistence.cs:116`

Drawn library. for (int labels = name.LabelCount - 1; ...) -> LabelCount + 1 survived: the search then tries 'ancestors' with more labels than the name, so the name itself can be taken as its own closest encloser (RFC 5155 section 8.3 requires a proper ancestor), changing the opt-out insecure-delegation decision.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.Dane.UnitLibrary/Lookup/DenialOfExistence.cs:116:-1 -Member ClosestEncloser -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `ProvesNameError_Nsec3MatchesOnlyTheNameItself_IsNotItsOwnClosestEncloser_Rfc5155Section8_3` to `Curl.Extension.Dane.UnitTests/Lookup/DenialOfExistenceTests.cs`: an NSEC3 matching the query name itself plus one covering every hash must not prove a name error, since the closest encloser must be a proper ancestor. No production change: the code was already right; only the test was missing.
- Reproduction: lanes may not run `Audit/Tools`, so the mutant was applied by hand (line 116, `LabelCount - 1` -> `LabelCount + 1`): the new test failed (killed), then the line was restored. Expected outcome "killed" now holds.
- Quality run skipped: no production code changed, only a test added. Build clean; fast tests green (115 test projects passed, 0 failed).

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. A test now kills the closest-encloser start mutant (AF-0595)
