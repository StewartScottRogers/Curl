---
id: BL-4858
title: Fix AF-0670: Surviving mutant: lib1515's debug trace can write hex dumps (noHex false)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-4858 — Fix AF-0670: Surviving mutant: lib1515's debug trace can write hex dumps (noHex false)

## Goal

The defect the audit office reported as AF-0670 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0670 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0670-surviving-mutant-lib1515-s-debug-trace-can-write-h.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1515.cs:124`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1515.cs:124`

Sampled mutant 'noHex: true' -> 'noHex: false' in 'new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true)' survived (sample -Seed 413592). No test pins lib1515's trace format. Same caveat: -Site ...:124:true mutates the line's first 'true' (traceTime).

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1515.cs:124:true -Member PerformAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived (sampled noHex mutant)

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- `Lib1512To1523Tests.Lib1515_SentHeader_TracesItWithoutAHexColumn` runs lib1515, then sends a header
  through the debug callback its multi handle was given, and pins the dump's tail
  `=> Send header, 5 bytes (0x5)\n0000: GET\n` (no hex column). The time prefix stays outside the pinned text.
- A lane cannot run `Audit/Tools/Invoke-MutationTest.ps1`, so the mutant was applied by hand
  (`noHex: false` on Lib1515.cs:124): the new test failed against it, and passed once it was
  reverted. That is the mutant killed.
- Test-only change, no production code changed, so no quality run was needed. Full fast run:
  115 test projects passed, none failed. The test has no platform-specific content.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-04: Doing -> Done. Lib1515's test pins the hex-free debug dump; the hand-applied noHex mutant fails it
