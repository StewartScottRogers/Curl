---
id: BL-5556
title: Fix AF-0885: Surviving mutant: the STOMP frame reader can double its buffer on every read and stop growing it when full
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Stomp.UnitLibrary/Wire/StompFrameReader.cs]
requirement: none
created: 2026-11-19
completed:
---
# BL-5556 — Fix AF-0885: Surviving mutant: the STOMP frame reader can double its buffer on every read and stop growing it when full

## Goal

The defect the audit office reported as AF-0885 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0885 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.Stomp.UnitLibrary/Wire/StompFrameReader.cs:137`

Location: `Curl.Extended.Protocol.Stomp.UnitLibrary/Wire/StompFrameReader.cs:137`

Mutant if (end == buffer.Length) -> if (end != buffer.Length) in FillAsync survived all Curl.Extended.Protocol.Stomp.UnitTests (seed 446316). With it, every fill that leaves room doubles the buffer, so a long subscription grows memory exponentially. A buffer that is exactly full is not grown, so ReadAsync gets an empty span, returns 0, and the reader reports the end of the connection inside a frame. ReadAsync_BodyLargerThanTheBuffer_GrowsTheBuffer passes either way, because no test counts the reads or reaches an exactly full buffer.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.Stomp.UnitLibrary/Wire/StompFrameReader.cs line 137` as the evidence above describes (the mutant operator is `==`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.Stomp.UnitLibrary/Wire/StompFrameReader.cs line 137`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-19: Created.
