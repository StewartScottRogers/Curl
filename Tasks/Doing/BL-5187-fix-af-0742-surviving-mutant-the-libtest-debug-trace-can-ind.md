---
id: BL-5187
title: Fix AF-0742: Surviving mutant: the libtest debug trace can index past data that ends in a carriage return
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-11-14
completed:
---
# BL-5187 — Fix AF-0742: Surviving mutant: the libtest debug trace can index past data that ends in a carriage return

## Goal

The defect the audit office reported as AF-0742 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0742 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitLibrary/LibTestDebugTrace.cs:123`

Location: `Curl.Conformance.LibTest.UnitLibrary/LibTestDebugTrace.cs:123`

Mutant noHex && index + 1 < data.Length && data[index] == CarriageReturn && data[index + 1] == LineFeed -> index + 1 <= data.Length survived (seed 641133). With noHex set and data whose last byte is CR, the mutant reads data[data.Length] and throws IndexOutOfRangeException, crashing the ported libtest's trace instead of writing the row as upstream's dump does. No LibTestDebugTrace test traces noHex data ending in a lone CR.

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/LibTestDebugTrace.cs line 123` as the evidence above describes (the mutant operator is `<`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/LibTestDebugTrace.cs line 123`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing. Criteria at claim: 550b24e3 9742157d.
