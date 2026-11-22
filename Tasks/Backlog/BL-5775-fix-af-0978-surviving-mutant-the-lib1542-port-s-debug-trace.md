---
id: BL-5775
title: Fix AF-0978: Surviving mutant: the lib1542 port's debug trace can dump with the hex column (noHex true -> false)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary/Lib1542.cs]
requirement: none
created: 2026-11-22
completed:
---
# BL-5775 — Fix AF-0978: Surviving mutant: the lib1542 port's debug trace can dump with the hex column (noHex true -> false)

## Goal

The defect the audit office reported as AF-0978 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0978 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1542.cs:49`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1542.cs:49`

Invoke-MutationTest -Library Curl.Conformance.LibTest.UnitLibrary -MaxMutants 40 -Seed 891023: survived. Mutated 'new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: false, noHex: true)' to 'noHex: false'. The port's stderr trace would gain upstream's hex column, which lib1542's upstream source does not print, and no test pins the trace format. This is the same gap as AF-0952 (lib3033) and AF-0967 (lib655).

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/Lib1542.cs line 49` as the evidence above describes (the mutant operator is `true`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/Lib1542.cs line 49`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-22: Created.
