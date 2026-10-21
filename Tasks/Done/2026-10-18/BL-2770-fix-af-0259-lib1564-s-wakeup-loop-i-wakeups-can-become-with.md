---
id: BL-2770
title: Fix AF-0259: Lib1564's wakeup loop i < wakeups can become <= with no test failing, sending one wakeup too many per round
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests]
requirement: none
created: 2026-10-18
completed: 2026-10-18
---
# BL-2770 — Fix AF-0259: Lib1564's wakeup loop i < wakeups can become <= with no test failing, sending one wakeup too many per round

## Goal

The defect the audit office reported as AF-0259 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0259 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0259-lib1564-s-wakeup-loop-i-wakeups-can-become-with-no.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1564.cs:65`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1564.cs:65`

LibTest sample, seed 0: survived. Original `for (int i = 0; i < wakeups; i++)`, mutated `<=`. A round meant to have no wakeup gets one, so curl_multi_poll returns early where upstream's lib1564 expects it to wait. No test pins the number of MultiWakeup calls per round.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1564.cs:65:< -Member TestAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `Curl.Conformance.LibTest.UnitTests` to `touches`: the fix is a test, and no task in Doing on `origin/work/dark-factory` names that project.
- Fix: `FakeMultiTransfer` counts wakeups (`Wakeups`); the new test `Lib1564_EachRound_SendsExactlyItsWakeupsBeforeItsPoll` pins the running count before each poll as 0, 1, 1, 11, 11. Production code is unchanged.
- The lane guard refuses the finding's mutation tool, so the mutant (`i <= wakeups` at `Lib1564.cs:65`) was applied by hand: the new test fails against it and passes once it is reverted. The re-audit runs the tool itself.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-18: Doing -> Done. Lib1564's wakeups per round are pinned by a test that kills the <= mutant
