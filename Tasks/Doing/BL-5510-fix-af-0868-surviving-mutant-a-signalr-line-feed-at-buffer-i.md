---
id: BL-5510
title: Fix AF-0868: Surviving mutant: a SignalR line feed at buffer index 0 is not seen as a line end
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/BufferedConnectionReader.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5510 — Fix AF-0868: Surviving mutant: a SignalR line feed at buffer index 0 is not seen as a line end

## Goal

The defect the audit office reported as AF-0868 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0868 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/BufferedConnectionReader.cs:26`

Location: `Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/BufferedConnectionReader.cs:26`

Mutant 'while ((lineFeed = Array.IndexOf(buffer, (byte)'\n', start, end - start)) < 0)' -> '<= 0' survived. A line whose LF is the buffer's first byte (an empty line read at offset 0) would make ReadLineAsync keep filling: a stall or misparse of the head. No test reads a line ending at index 0.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/BufferedConnectionReader.cs line 26` as the evidence above describes (the mutant operator is `<`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/BufferedConnectionReader.cs line 26`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing. Criteria at claim: 816f0cd6 9742157d.
