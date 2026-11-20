---
id: BL-5623
title: Fix AF-0908: Surviving mutant: the lib557 port can leave out every lowest-nibble unsigned printf case
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary/Lib557.cs]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-5623 — Fix AF-0908: Surviving mutant: the lib557 port can leave out every lowest-nibble unsigned printf case

## Goal

The defect the audit office reported as AF-0908 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0908 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib557.cs:193`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib557.cs:193`

Invoke-MutationTest -Library Curl.Conformance.LibTest.UnitLibrary -MaxMutants 40 -Seed 912464: 'for (int shift = bits - 4; shift >= 0; shift -= 4)' in Unsigned mutated to 'shift > 0' survived. The table's own comment says it holds each of the nibbles F, E, C and 1 at every nibble position. The mutant drops the shift-0 rows (0xF, 0xE, 0xC, 0x1) from every width, so the port checks fewer curl_mprintf cases than upstream's lib557, and a formatting bug in the lowest digit would go unseen.

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/Lib557.cs line 193` as the evidence above describes (the mutant operator is `>=`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/Lib557.cs line 193`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Killed by Lib557Tests.Groups_UnsignedWidth_HoldsEachNibbleAtEveryPositionIncludingTheLowest (checked by hand: shift > 0 fails it, reverted passes; project 1231 pass). Only tests changed, no production code.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing. Criteria at claim: 7d4d1851 9742157d.
- 2026-11-20: Doing -> Done. Added test killing the shift mutant
