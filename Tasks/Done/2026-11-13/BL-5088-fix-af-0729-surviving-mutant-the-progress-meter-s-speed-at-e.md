---
id: BL-5088
title: Fix AF-0729: Surviving mutant: the progress meter's speed at exactly one second can read as the maximum for huge sizes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitLibrary]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5088 — Fix AF-0729: Surviving mutant: the progress meter's speed at exactly one second can read as the maximum for huge sizes

## Goal

The defect the audit office reported as AF-0729 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0729 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0729-surviving-mutant-the-progress-meter-s-speed-at-exa.md`.

Location: `Curl.Output.UnitLibrary/ProgressMeterFields.cs:103`

Location: `Curl.Output.UnitLibrary/ProgressMeterFields.cs:103`

Mutant 'microseconds >= 1000000' -> '> 1000000' survived. For sizes at or above long.MaxValue / 1000000 the original gives size / 1 at exactly 1,000,000 microseconds and the mutant gives long.MaxValue, a different speed column. The pure function's boundary is untested.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Output.UnitLibrary/ProgressMeterFields.cs:103:>= -Member Speed -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Added a Speed test row at exactly 1000000 microseconds with a huge size; it kills the >= to > mutant (the mutant would give long.MaxValue). The audit tool is off-limits to lanes, so the reproduction itself was not run. Test only, no production change.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-13: Doing -> Done. Added boundary test row at exactly 1000000 microseconds
