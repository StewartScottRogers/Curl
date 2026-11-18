---
id: BL-5509
title: Fix AF-0867: Surviving mutant: a SignalR status line of two tokens can be refused
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/HttpResponseReader.cs]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5509 — Fix AF-0867: Surviving mutant: a SignalR status line of two tokens can be refused

## Goal

The defect the audit office reported as AF-0867 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0867 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/HttpResponseReader.cs:44`

Location: `Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/HttpResponseReader.cs:44`

Mutant 'if (parts.Length < 2 || ...)' -> 'parts.Length <= 2' survived. A status line with no reason phrase ('HTTP/1.1 200') would throw 'malformed HTTP status line', failing the negotiate or upgrade with an error exit instead of going on. No test sends a two-token status line. This is the same untested boundary AF-0845 found in SocketIoUpgrade.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/HttpResponseReader.cs line 44` as the evidence above describes (the mutant operator is `<`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/HttpResponseReader.cs line 44`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by `HttpResponseReaderTests.ReadHeadAsync_StatusLineWithoutReasonPhrase_ReadsTheStatusCode_Rfc9112Section4` (new, sends `HTTP/1.1 200`). Verified by hand: with `< 2` -> `<= 2` it fails; reverted, all 155 pass. No production change.

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing. Criteria at claim: 6ef977f0 9742157d.
- 2026-11-18: Doing -> Done. Test added that kills the mutant
