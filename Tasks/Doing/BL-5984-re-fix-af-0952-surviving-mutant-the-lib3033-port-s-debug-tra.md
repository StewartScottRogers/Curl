---
id: BL-5984
title: Re-fix AF-0952: Surviving mutant: the lib3033 port's debug trace can dump with the hex column (noHex true -> false)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary/Lib3033.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5984 — Re-fix AF-0952: Surviving mutant: the lib3033 port's debug trace can dump with the hex column (noHex true -> false)

## Goal

The defect the audit office reported as AF-0952 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0952 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Re-fix: the earlier task(s) BL-5721, BL-5731 reached Done, and a later re-audit by the quality auditor found the reproduction still reproduces:

- 2026-11-25 | 2026-11-25_1306.md | reproduces: yes (runner rerun on clean commit) | Ran the -Site command: line 59, 'traceTime: true, noHex: true' -> 'traceTime: false, noHex: true', outcome survived. The ':true' operator changes the line's first 'true' (traceTime), not the noHex the title names; the reproduction as written still survives. Runner's targeted mutation rerun on the clean audited commit: survived.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib3033.cs:59`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib3033.cs:59`

Line 59: var trace = new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true); mutated to noHex: false survived the whole Curl.Conformance.LibTest.UnitTests run. LibTestDebugTrace.DebugDump (LibTestDebugTrace.cs:58-63) writes 16-byte rows with a hex column instead of 64-byte text rows when noHex is false, so the port's stderr bytes change and no test pins lib3033's trace output.

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/Lib3033.cs line 59` as the evidence above describes (the mutant operator is `true`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/Lib3033.cs line 59`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 90d30022 9742157d.
