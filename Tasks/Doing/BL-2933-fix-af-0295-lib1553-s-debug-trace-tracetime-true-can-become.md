---
id: BL-2933
title: Fix AF-0295: Lib1553's debug trace traceTime: true can become false with no test failing, dropping the timestamps from its stderr trace
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests/Lib1551To1559Tests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2933 — Fix AF-0295: Lib1553's debug trace traceTime: true can become false with no test failing, dropping the timestamps from its stderr trace

## Goal

The defect the audit office reported as AF-0295 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0295 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0295-lib1553-s-debug-trace-tracetime-true-can-become-fa.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1553.cs:72`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1553.cs:72`

Sampled mutant (seed 0) survived: new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true) => traceTime: false. The trace lib1553 writes to stderr would lose the time prefix upstream's debug callback writes, and no test checks Lib1553's trace text. This is the same kind of gap as AF-0274 (Lib552 noHex), in another port.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1553.cs:72:true -Member TestAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-21 (lane 4): only a test kills the mutant at `Lib1553.cs:72`, so `touches` now also names `Curl.Conformance.LibTest.UnitTests/Lib1551To1559Tests.cs`. Plan: add a test there that calls the Debug callback Lib1553 sets, the way `Lib552Tests.RunAsync_TracesSentDataAsTextWithNoHexAndNoTime` does for AF-0274, and asserts that the stderr line starts with a `HH:mm:ss.ffffff ` time prefix and holds no hex. Requeued because BL-2930 (Doing) touches the whole `Curl.Conformance.LibTest.UnitTests`.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-21: Doing -> Backlog. Needs Curl.Conformance.LibTest.UnitTests/Lib1551To1559Tests.cs, which BL-2930 (Doing) holds through its whole-project touches on Curl.Conformance.LibTest.UnitTests
- 2026-10-21: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
