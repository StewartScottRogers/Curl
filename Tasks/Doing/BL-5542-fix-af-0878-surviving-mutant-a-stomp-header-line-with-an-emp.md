---
id: BL-5542
title: Fix AF-0878: Surviving mutant: a STOMP header line with an empty name (':value') can be refused as having no colon
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Stomp.UnitLibrary/Wire/StompFrameEncoding.cs]
requirement: none
created: 2026-11-19
completed:
---
# BL-5542 — Fix AF-0878: Surviving mutant: a STOMP header line with an empty name (':value') can be refused as having no colon

## Goal

The defect the audit office reported as AF-0878 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0878 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.Stomp.UnitLibrary/Wire/StompFrameEncoding.cs:75`

Location: `Curl.Extended.Protocol.Stomp.UnitLibrary/Wire/StompFrameEncoding.cs:75`

Mutant 'if (colon <= 0)' (from '<') survived in the sample and in the targeted -Site run. A header line starting with ':' is read today as StompHeader("", value). Under the mutant it throws FormatException 'STOMP header line without a colon', failing the transfer. ParseHeader_NoColon_IsAFormatError covers only 'nocolon'; no test parses a line whose colon is at index 0.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.Stomp.UnitLibrary/Wire/StompFrameEncoding.cs line 75` as the evidence above describes (the mutant operator is `<`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.Stomp.UnitLibrary/Wire/StompFrameEncoding.cs line 75`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing. Criteria at claim: e58e57cf 9742157d.
