---
id: BL-4885
title: Fix AF-0691: Surviving mutant: lib753's debug trace can write hex dumps (noHex false)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-11-06
completed: 2026-11-06
---
# BL-4885 — Fix AF-0691: Surviving mutant: lib753's debug trace can write hex dumps (noHex false)

## Goal

The defect the audit office reported as AF-0691 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0691 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0691-surviving-mutant-lib753-s-debug-trace-can-write-he.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib753.cs:41`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib753.cs:41`

Invoke-MutationTest (Curl.Conformance.LibTest.UnitLibrary, -MaxMutants 40, -Seed 982898) changed '... new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true));' to '... noHex: false));' and every LibTest.UnitTests test passed (survived). I confirmed this by hand in a scratch worktree: 1205/1205 passed. lib753's stderr trace would gain hex dump columns unnoticed, unlike lib500/1515/1522/1540/2700, whose noHex mutants are now killed. Note: the line has two 'true' sites. The -Site form takes the first (traceTime), so a site rerun mutates traceTime, not this noHex mutant.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib753.cs:41:true -Member TestAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `Lib753_SentHeader_TracesItWithTheTimeAndNoHexColumn` to `Curl.Conformance.LibTest.UnitTests/Lib751To1398Tests.cs`: it calls lib753's debug callback with a sent header and pins the whole trace line, `14:05:06.123456 => Send header, 5 bytes (0x5)` then `0000: GET`, so a mutant of either `noHex` or `traceTime` fails it. The test sits in the test project of the `touches` library; production code is unchanged, so no quality run was needed.
- A lane may not run `Audit/Tools/Invoke-MutationTest.ps1`, so the reproduction was done by hand: with `noHex: false` in `Lib753.cs:41`, the new test failed (killed). After restoring the line, all 1206 tests in the project passed.

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-06: Doing -> Done. lib753's trace test pins its time stamp and hex-free dump, killing the noHex mutant
