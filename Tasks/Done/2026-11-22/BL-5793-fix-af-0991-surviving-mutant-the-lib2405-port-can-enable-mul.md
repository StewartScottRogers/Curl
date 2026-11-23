---
id: BL-5793
title: Fix AF-0991: Surviving mutant: the lib2405 port can enable multiplexing for the HTTP/1 runs and not the HTTP/2 run (== -> !=)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary/Lib2405Run.cs]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-5793 — Fix AF-0991: Surviving mutant: the lib2405 port can enable multiplexing for the HTTP/1 runs and not the HTTP/2 run (== -> !=)

## Goal

The defect the audit office reported as AF-0991 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0991 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib2405Run.cs:134`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib2405Run.cs:134`

Seeded mutation run: 'if (use == Lib2405.UseHttp2Multiplex)' -> 'if (use != Lib2405.UseHttp2Multiplex)' survived. The branch sets CURLMOPT_PIPELINING to multiplex; the mutant inverts which lib2405 variants multiplex. No test checks the multi option set for each variant.

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/Lib2405Run.cs line 134` as the evidence above describes (the mutant operator is `==`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/Lib2405Run.cs line 134`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by `Lib2405_Case2405_HoldsTwoDescriptorsOverHttp1`, which now asserts the HTTP/1 run writes no multi option; `Lib2405_Case2407_RunsHttp2ThenMultiplexed` asserts exactly one write, pipelining multiplex. Mutant applied by hand: 1 failure; reverted: 1242 pass. No production change.

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: 7b18f9a0 9742157d.
- 2026-11-22: Doing -> Done. Killing test added
