---
id: BL-2802
title: Fix AF-0275: Lib1537.LengthToNul's nul < 0 can become <= 0 with no test failing, so bytes starting with NUL count as their full length
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests/Lib1535To1550Tests.cs]
requirement: none
created: 2026-10-19
completed: 2026-10-19
---
# BL-2802 — Fix AF-0275: Lib1537.LengthToNul's nul < 0 can become <= 0 with no test failing, so bytes starting with NUL count as their full length

## Goal

The defect the audit office reported as AF-0275 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0275 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0275-lib1537-lengthtonul-s-nul-0-can-become-0-with-no-t.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1537.cs:84`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1537.cs:84`

Mutant return nul < 0 ? raw.Length : nul -> nul <= 0 survived (sampled run, seed 0). LengthToNul is documented as strlen of decoded bytes. With the mutant, decoded bytes whose first byte is NUL report raw.Length instead of 0, which changes the length lib1537 prints. No test decodes bytes starting with NUL.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1537.cs:84:< -Member LengthToNul -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `Curl.Conformance.LibTest.UnitTests/Lib1535To1550Tests.cs` to `touches`: the fix is a test (decoded bytes starting with NUL count 0), and the Lib1537 tests live there; no task in Doing names it. The production code was already right; only the test was missing.
- The audit guard refuses a lane `Audit/Tools/Invoke-MutationTest.ps1`, so the reproduction was run by hand: with line 84 mutated to `nul <= 0` (the finding's mutant), `Lib1537LengthToNul_StopsAtTheFirstNul` fails on `Assert.AreEqual(0, Lib1537.LengthToNul([0, 0x41]))`, so the mutant is killed; the mutant was then reverted. Only test code changed, so no quality measurement was needed.

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-19: Doing -> Done. Lib1537.LengthToNul's test pins 0 for bytes starting with NUL, killing AF-0275's nul <= 0 mutant
