---
id: BL-5251
title: Fix AF-0800: Surviving mutant: lib757's read callback can send nothing when the buffer is exactly the part's length
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-11-15
completed:
---
# BL-5251 — Fix AF-0800: Surviving mutant: lib757's read callback can send nothing when the buffer is exactly the part's length

## Goal

The defect the audit office reported as AF-0800 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0800 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib757.cs:29`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib757.cs:29`

The seed 770877 sample of Curl.Conformance.LibTest.UnitLibrary (one of the three largest): 'if (buffer.Length < Data.Length)' -> 'if (buffer.Length <= Data.Length)' in Read survived. A buffer of exactly 24 bytes, the length of '<title>fun-times</title>', then gets 0 bytes instead of the part, so the uploaded MIME part goes out empty. No test calls Read with a buffer of exactly Data.Length, so the 'whole of Data when it fits' boundary the doc comment states is unchecked.

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/Lib757.cs line 29` as the evidence above describes (the mutant operator is `<`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/Lib757.cs line 29`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-15: Created.
