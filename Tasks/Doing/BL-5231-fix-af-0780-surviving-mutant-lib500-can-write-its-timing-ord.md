---
id: BL-5231
title: Fix AF-0780: Surviving mutant: lib500 can write its timing-order complaint lines when two times are equal
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-11-15
completed:
---
# BL-5231 — Fix AF-0780: Surviving mutant: lib500 can write its timing-order complaint lines when two times are equal

## Goal

The defect the audit office reported as AF-0780 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0780 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib500.cs:118`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib500.cs:118`

if (earlier > later) { file.WriteLine(...) } mutated to >= survived. Upstream lib500 writes the 'X vs Y' line only when the earlier time is larger. Equal times are common (a literal address has namelookup == connect), so the mutant would put lines into the libtest_arg2 file that upstream leaves empty, and no test checks that file for equal times.

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/Lib500.cs line 118` as the evidence above describes (the mutant operator is `>`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/Lib500.cs line 118`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing. Criteria at claim: 4a80b59d 9742157d.
