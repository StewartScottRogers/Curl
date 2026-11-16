---
id: BL-5276
title: Fix AF-0822: Surviving mutant: libtest's default verbose trace can write text starting with LF as one prefixed line
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary/LibTestDefaultVerboseTrace.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5276 — Fix AF-0822: Surviving mutant: libtest's default verbose trace can write text starting with LF as one prefixed line

## Goal

The defect the audit office reported as AF-0822 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0822 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitLibrary/LibTestDefaultVerboseTrace.cs:43`

Location: `Curl.Conformance.LibTest.UnitLibrary/LibTestDefaultVerboseTrace.cs:43`

`end = end < 0 ? text.Length : end + 1` changed to `end <= 0` survived. For text whose first byte is '\n' (IndexOf gives 0), the original writes the prefix and '\n', then the prefix and the rest. The mutant writes the whole text after one prefix, changing the trace output. Trace_TextAndHeaders_PrefixesEachLineAsLibcurlsDefaultVerboseOutputDoes has no text starting with LF.

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/LibTestDefaultVerboseTrace.cs line 43` as the evidence above describes (the mutant operator is `<`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/LibTestDefaultVerboseTrace.cs line 43`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: 04a16ee1 9742157d.
