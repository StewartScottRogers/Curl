---
id: BL-5985
title: Re-fix AF-0967: Surviving mutant: the lib655 port's debug trace can dump with the hex column (noHex true -> false)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary/Lib655.cs]
requirement: none
created: 2026-11-25
completed:
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

- [ ] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/Lib655.cs line 46`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 5f7f3096 9742157d.
