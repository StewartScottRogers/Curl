---
id: BL-5274
title: Fix AF-0820: Surviving mutant: lib1576's read callback can send an unfilled buffer when it is exactly the body's length
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary/Lib1576.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5274 — Fix AF-0820: Surviving mutant: lib1576's read callback can send an unfilled buffer when it is exactly the body's length

## Goal

The defect the audit office reported as AF-0820 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0820 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1576.cs:79`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1576.cs:79`

`if (buffer.Length >= Body.Length)` changed to `>` survived. With a buffer exactly the body's length, the body is not copied but Body.Length is still returned, so the upload sends whatever the buffer held. Lib1576_Read_CopiesTheBodyOnEveryCallUnlessTheBufferIsTooSmall only uses 100- and 10-byte buffers. This is the same boundary as AF-0800 in Lib757.

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/Lib1576.cs line 79` as the evidence above describes (the mutant operator is `>=`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/Lib1576.cs line 79`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by new test `Lib1576_Read_CopiesTheBodyIntoABufferExactlyItsLength` (verified by hand with `>=` changed to `>`, then reverted). Build clean, LibTest fast tests 1217 pass. Test project only; no production change.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: e6b2ca00 9742157d.
