---
id: BL-5985
title: Re-fix AF-0967: Surviving mutant: the lib655 port's debug trace can dump with the hex column (noHex true -> false)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary/Lib655.cs, Curl.Conformance.LibTest.UnitTests/Lib643To661Tests.cs]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5985 — Re-fix AF-0967: Surviving mutant: the lib655 port's debug trace can dump with the hex column (noHex true -> false)

## Goal

The defect the audit office reported as AF-0967 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0967 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Re-fix: the earlier task(s) BL-5738 reached Done, and a later re-audit by the quality auditor found the reproduction still reproduces:

- 2026-11-25 | 2026-11-25_1306.md | reproduces: yes (runner rerun on clean commit) | Ran the -Site command: Lib655.cs line 46, 'traceTime: true' -> 'traceTime: false' (first 'true' on the line), outcome survived. As with AF-0952 the operator hits traceTime, not noHex. Runner's targeted mutation rerun on the clean audited commit: survived.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib655.cs:46`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib655.cs:46`

Seeded sample (seed 51850, -MaxMutants 40): the mutant at Lib655.cs:46 operator 'true' survived. Original: 'var trace = new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true);'; mutated: 'noHex: false'. upstream's lib655 traces without the hex column, so the port's standard error bytes would differ from the case's expected output, and no Lib655 unit test pins the trace format. This is the same gap AF-0952 records for Lib3033.cs:59, which still survives this audit.

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/Lib655.cs line 46` as the evidence above describes (the mutant operator is `true`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/Lib655.cs line 46`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Killing test: `Lib643To661Tests.Lib655_TracesDataWithTheTimeAndWithoutTheHexColumn`
  (renamed from `Lib655_TracesDataWithoutTheHexColumn`). It now asserts that standard error
  starts with `00:00:00.000000 => Send data, 2 bytes (0x2)\n0000: ok\n`, which pins both
  `traceTime: true` and `noHex: true`. The old assertions pinned only the missing hex column.
- By hand, 2026-11-25: with `traceTime: true` changed to `false` at Lib655.cs:46, that test
  failed (1 failed, 1242 passed in `Curl.Conformance.LibTest.UnitTests`). Reverted: `dotnet build`
  gave 0 warnings and 0 errors, and the full fast tests passed.
- The title names `noHex` but the audit's mutation hits `traceTime`, the first `true` on the line;
  the one assertion kills either.
- No production code changed, so no ADR and no quality run. The `feature` pipeline stages were
  not delegated: the change is one test assertion. `touches` gained the test file; no other task
  in Doing on `origin/work/dark-factory` named it.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 5f7f3096 9742157d.
- 2026-11-25: Doing -> Done. A fast test now pins lib655's trace time prefix and missing hex column, killing the AF-0967 mutant
