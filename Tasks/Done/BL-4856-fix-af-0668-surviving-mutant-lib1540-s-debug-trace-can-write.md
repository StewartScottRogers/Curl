---
id: BL-4856
title: Fix AF-0668: Surviving mutant: lib1540's debug trace can write hex dumps (noHex false)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests/Lib1535To1550Tests.cs]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-4856 — Fix AF-0668: Surviving mutant: lib1540's debug trace can write hex dumps (noHex false)

## Goal

The defect the audit office reported as AF-0668 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0668 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0668-surviving-mutant-lib1540-s-debug-trace-can-write-h.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1540.cs:51`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1540.cs:51`

Sampled mutant 'noHex: true' -> 'noHex: false' in 'new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true)' survived (sample -Library Curl.Conformance.LibTest.UnitLibrary -MaxMutants 40 -Seed 413592). Upstream lib1540 sets nohex, and the change alters every data line this port writes to stderr. No test pins the trace format. Caveat: on this line -Site ...:51:true resolves to the first 'true' (traceTime), not noHex, so the targeted command below does not mutate this site; the sample command does.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1540.cs:51:true -Member TestAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived (sampled noHex mutant)

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Fix: the mutant is a gap in the tests, not a bug in `Lib1540.cs`, which already passes `noHex: true` as upstream's lib1540 does. `Lib1540_SetsItsCallbacksAndTraceAndReturnsTheTransfersCode` now has the fake engine send a `HeaderOut` debug call (`GET` CR LF) and pins the dump's end: the title line `=> Send header, 5 bytes (0x5)`, then the row `0000: GET`. Without the hex column the CR LF ends the row early; with `noHex: false` the row would carry `47 45 54 0d 0a ...`.
- Touches: added `Curl.Conformance.LibTest.UnitTests/Lib1535To1550Tests.cs`, because the test lives there. No task in Doing on origin/work/dark-factory names it.
- Reproduction: lanes may not run `Audit/Tools/Invoke-MutationTest.ps1`. Instead I flipped line 51 to `noHex: false` by hand, and the Lib1540 tests failed (1 of 4: this test). I reverted the flip, and they pass again. Production code is unchanged, so no Measure-CodeQuality run was needed.
- `dotnet build`: 0 warnings, 0 errors. Fast tests: no project failed. The change uses no file system, path or OS behaviour, so it is platform-neutral.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-04: Doing -> Done. Lib1540's test pins the hex-free debug dump; the noHex mutant is killed
