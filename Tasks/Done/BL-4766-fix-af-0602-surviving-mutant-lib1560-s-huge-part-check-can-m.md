---
id: BL-4766
title: Fix AF-0602: Surviving mutant: lib1560's huge-part check can make every part but one huge without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests/Lib1560ChecksTests.cs]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4766 — Fix AF-0602: Surviving mutant: lib1560's huge-part check can make every part but one huge without a test failing

## Goal

The defect the audit office reported as AF-0602 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0602 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0602-surviving-mutant-lib1560-s-huge-part-check-can-mak.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1560Checks.Handles.cs:108`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1560Checks.Handles.cs:108`

piece == i ? BigPart : "c" -> piece != i survived: the check then builds URLs with six huge parts instead of one, a different upstream check whose error count reaches lib1560's output; no test pins Huge's result.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1560Checks.Handles.cs:108:== -Member Huge -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The fix is test-only: `Lib1560ChecksTests` gains `Huge_EachUrl_MakesOnlyItsOwnPartHuge` and `Huge_SchemeParsed_WritesTheUrlWithOnlyAHugeScheme`, which pin the exact URL text Huge writes for each part, so a URL with more than one huge part fails them. No production code changed.
- Added `Curl.Conformance.LibTest.UnitTests/Lib1560ChecksTests.cs` to `touches`: the test belongs there, and no task in Doing on origin/work/dark-factory names that project.
- Lanes may not run `Audit/Tools/Invoke-MutationTest.ps1`, so the criterion was checked by hand: with line 108 changed to `piece != i` (the finding's mutant), both new tests failed (2 failed, 1 passed); with the line restored, all 1194 tests in Curl.Conformance.LibTest.UnitTests pass. The re-audit confirms it with the script.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. lib1560's Huge check now has tests pinning each URL's single huge part, killing AF-0602's mutant
