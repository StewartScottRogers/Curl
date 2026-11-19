---
id: BL-5543
title: Fix AF-0879: Surviving mutant: a STOMP blank line at the start of a refilled buffer can read buffer[-1]
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Stomp.UnitLibrary/Wire/StompFrameReader.cs]
requirement: none
created: 2026-11-19
completed:
---
# BL-5543 — Fix AF-0879: Surviving mutant: a STOMP blank line at the start of a refilled buffer can read buffer[-1]

## Goal

The defect the audit office reported as AF-0879 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0879 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.Stomp.UnitLibrary/Wire/StompFrameReader.cs:80`

Location: `Curl.Extended.Protocol.Stomp.UnitLibrary/Wire/StompFrameReader.cs:80`

Mutant 'int length = newline >= start && buffer[newline - 1] == '\r' ? ...' (from '>') survived in the sample and in the targeted -Site run. FillAsync compacts the buffer so that start = 0 (lines 130-134). When the header block's ending blank line '\n' arrives as the first byte of a refill, newline == start == 0. The mutant then reads buffer[-1] and throws IndexOutOfRangeException, ending a transfer that should succeed with a different exit code. ReadAsync_FrameSplitAcrossReads_IsReassembled does not split the frame at the blank line.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.Stomp.UnitLibrary/Wire/StompFrameReader.cs line 80` as the evidence above describes (the mutant operator is `>`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.Stomp.UnitLibrary/Wire/StompFrameReader.cs line 80`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing. Criteria at claim: e034913d 9742157d.
