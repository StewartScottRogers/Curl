---
id: BL-4765
title: Fix AF-0601: Surviving mutant: lib591 can refuse an accept timeout of 0 without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests/Lib578To599Tests.cs]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4765 — Fix AF-0601: Surviving mutant: lib591 can refuse an accept timeout of 0 without a test failing

## Goal

The defect the audit office reported as AF-0601 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0601 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0601-surviving-mutant-lib591-can-refuse-an-accept-timeo.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib591.cs:66`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib591.cs:66`

seconds is >= 0 -> > 0 survived: an argument of '0' is then refused as not a timeout, a decision the port makes from its command line.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib591.cs:66:>= -Member TryParseAcceptTimeout -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `[DataRow("0", true, 0L)]` to `Lib591_TryParseAcceptTimeout_TakesDecimalDigitsUpTo65535`: with the mutant `seconds is > 0` that row gets `false` and the test fails, so the mutant is killed. No production change. A lane may not run `Audit/Tools/Invoke-MutationTest.ps1`; the first box is ticked by that reasoning, and the quality auditor's re-audit confirms it.
- Widened `touches` with `Curl.Conformance.LibTest.UnitTests/Lib578To599Tests.cs`, where the test lives; no task in Doing on work/dark-factory names it.
- `dotnet build` clean; fast tests green (Curl.Conformance.LibTest.UnitTests 1192 passed, no failures in the whole fast run). No quality run: no production code changed.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. lib591's accept-timeout test now pins 0 as a valid timeout, killing the >= to > mutant
