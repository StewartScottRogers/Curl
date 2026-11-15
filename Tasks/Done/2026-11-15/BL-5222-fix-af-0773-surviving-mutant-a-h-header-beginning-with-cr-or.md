---
id: BL-5222
title: Fix AF-0773: Surviving mutant: a -H header beginning with CR or LF can be sent whole instead of cut to its empty first line
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.WebTransport.UnitLibrary]
requirement: none
created: 2026-11-14
completed: 2026-11-14
---
# BL-5222 — Fix AF-0773: Surviving mutant: a -H header beginning with CR or LF can be sent whole instead of cut to its empty first line

## Goal

The defect the audit office reported as AF-0773 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0773 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.WebTransport.UnitLibrary/Session/WebTransportConnectHeaderFields.cs:89`

Location: `Curl.Extended.Protocol.WebTransport.UnitLibrary/Session/WebTransportConnectHeaderFields.cs:89`

Mutant 'return end < 0 ? header : header[..end];' -> 'end <= 0' survived. When the header starts with CR or LF (end == 0), the original keeps only the empty first line, so the header is ignored. The mutant keeps the whole text, so the lines after the CRLF reach ApplyCustomHeader. Build_CustomHeaders_ColonRemovesSemicolonSendsEmptyAndOnlyTheFirstLineCounts covers a line break later in the header, never at index 0. This decides which fields the CONNECT sends.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.WebTransport.UnitLibrary/Session/WebTransportConnectHeaderFields.cs line 89` as the evidence above describes (the mutant operator is `<`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.WebTransport.UnitLibrary/Session/WebTransportConnectHeaderFields.cs line 89`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Test-only change: no production code changed, so no quality run. Killed by Build_CustomHeaderStartingWithLineBreak_IsIgnoredWhole (2 rows, CR LF and LF); with the mutant applied by hand both rows failed, reverted and all 89 pass.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing. Criteria at claim: 288c5350 9742157d.
- 2026-11-14: Doing -> Done. Mutant killed by new test
