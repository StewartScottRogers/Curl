---
id: BL-5303
title: Fix AF-0829: Surviving mutant: lib753 can enter its transfer loop after a failed first perform
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary/Lib753.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5303 — Fix AF-0829: Surviving mutant: lib753 can enter its transfer loop after a failed first perform

## Goal

The defect the audit office reported as AF-0829 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0829 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib753.cs:120`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib753.cs:120`

Mutation sample (seed 413266): 'goesOn = goesOn && running > 0;' mutated to 'goesOn = goesOn || running > 0;' survived. With the mutant, a first curl_multi_perform that failed while a transfer is still running enters the swap/wait loop instead of stopping, and one with nothing running still turns once, so the program's output and exit code after a failed perform differ from upstream's lib753.

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/Lib753.cs line 120` as the evidence above describes (the mutant operator is `&&`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/Lib753.cs line 120`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-16: Created.
