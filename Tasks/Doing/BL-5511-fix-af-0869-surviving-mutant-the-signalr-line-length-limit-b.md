---
id: BL-5511
title: Fix AF-0869: Surviving mutant: the SignalR line-length limit boundary is untested
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/BufferedConnectionReader.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5511 — Fix AF-0869: Surviving mutant: the SignalR line-length limit boundary is untested

## Goal

The defect the audit office reported as AF-0869 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0869 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/BufferedConnectionReader.cs:28`

Location: `Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/BufferedConnectionReader.cs:28`

Mutant 'if (end - start >= MaxLineLength)' -> '>' survived. A head line of exactly MaxLineLength (64 KiB) bytes with no LF yet is refused unmutated but read further under the mutant. ReadHeadAsync_LineLongerThanTheLimit_Throws uses a line past the limit only, so the refusal boundary can move unnoticed.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/BufferedConnectionReader.cs line 28` as the evidence above describes (the mutant operator is `>=`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/BufferedConnectionReader.cs line 28`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing. Criteria at claim: 50544457 9742157d.
