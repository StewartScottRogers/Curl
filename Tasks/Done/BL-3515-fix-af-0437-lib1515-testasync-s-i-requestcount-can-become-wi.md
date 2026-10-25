---
id: BL-3515
title: Fix AF-0437: Lib1515.TestAsync's i < RequestCount can become <= with no test failing, adding a DNS-timeout wait after the last request
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests/Lib1512To1523Tests.cs]
requirement: none
created: 2026-10-25
completed: 2026-10-25
---
# BL-3515 — Fix AF-0437: Lib1515.TestAsync's i < RequestCount can become <= with no test failing, adding a DNS-timeout wait after the last request

## Goal

The defect the audit office reported as AF-0437 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0437 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0437-lib1515-testasync-s-i-requestcount-can-become-with.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1515.cs:74`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1515.cs:74`

Sampled mutant (seed 0) survived: 'if (i < RequestCount)' -> 'if (i <= RequestCount)'. Upstream lib1515.c waits (DNS timeout + 1) seconds only between requests, never after the last one. With the mutant the port also waits after the last request, so upstream cases 1515 and 1516 run seconds longer on a real clock. Curl.Conformance.LibTest.UnitTests never checks how many clock waits the port makes.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1515.cs:74:< -Member TestAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `Curl.Conformance.LibTest.UnitTests/Lib1512To1523Tests.cs` to touches: the fix is a test (the port already matches upstream lib1515.c), and no other task in Doing on origin/work/dark-factory names that file.
- Fix: new test `Lib1515_WaitsTheDnsTimeoutPlusOneSecondOnlyBetweenTheRequests` runs the port on a TimeProvider that fires every timer at once and records its due time, and asserts exactly RequestCount - 1 waits of (DNS timeout + 1) s. No production change.
- Criterion 1: a lane may not run the audit office tools, so the mutant was applied by hand (`i < RequestCount` -> `i <= RequestCount` at Lib1515.cs:74): the new test failed (killed), and passes with the code restored. The re-audit confirms with its own tool.

## Log

- 2026-10-25: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-25: Doing -> Done. A unit test now pins that Lib1515 waits the DNS timeout + 1 s only between requests, killing the i <= RequestCount mutant
