---
id: BL-4844
title: Fix AF-0662: Surviving mutant: lib1522's debug trace can write hex dumps (noHex false)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests/Lib1512To1523Tests.cs]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-4844 — Fix AF-0662: Surviving mutant: lib1522's debug trace can write hex dumps (noHex false)

## Goal

The defect the audit office reported as AF-0662 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0662 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0662-surviving-mutant-lib1522-s-debug-trace-can-write-h.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1522.cs:63`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1522.cs:63`

Mutant 'noHex: true' -> 'noHex: false' on its LibTestDebugTrace survived. The port's stderr trace would then carry hex dumps that upstream lib1522 does not write. No test pins that trace.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1522.cs:63:true -Member TestAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes
- Cause: no test reached lib1522's debug callback, so its trace's noHex flag was unpinned. Added Lib1522_TracesSentDataAsTextWithTheTimeAndNoHex to Curl.Conformance.LibTest.UnitTests/Lib1512To1523Tests.cs (the same check Lib1553 has): the fake engine's perform calls the Debug callback with DataOut "ok" and the test pins "14:05:06.123456 => Send data, 2 bytes (0x2)\n0000: ok\n" on stderr. Production code is unchanged, so no quality run was needed.
- Widened touches to that test file because the fix is a test. No other task in Doing on origin/work/dark-factory names it.
- Reproduction: a lane may not run the audit office's mutation tool, so I applied the mutant by hand (noHex: true -> false at Lib1522.cs:63) and ran the Lib1522 tests. The new test failed (1 of 4), so the mutant is killed. I then restored the source. dotnet build is clean (0 warnings). The fast tests exited 0, with 114 projects passing and none failing.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-04: Doing -> Done. lib1522's debug trace is pinned to text with no hex dump; the noHex mutant is killed
