---
id: BL-185
title: Report HTTP transfer progress through the transfer progress sink
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-173, BL-134]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-185 — Report HTTP transfer progress through the transfer progress sink

## Goal

The HTTP handler reports transfer start, totals and counters through the progress sink BL-134 adds, per BL-133's ADR.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item H17. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-133 (ADR) and BL-134 (contract) define the sink; BL-129 does the same for file://.

## Acceptance criteria

- [x] Tests show the sink receives start, expected download total (Content-Length or unknown), and running byte counts for a scripted exchange, per BL-134's contract.
- [x] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes` (added by BL-169), never a socket; every parser test also runs with 1-byte chunks.

## Notes

- Plan item: H17 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- 2026-09-27 (lane 1): added `Documentation/Planning/Decisions` to `touches` for ADR-0065, the decision ADR-0045 left to the first handler that resends a body; no task in Doing names it.
- Decided (ADR-0065, by Claude under Stewart's delegation): "started" once the first connection is made (a connect failure reports nothing); download counts are the bytes the output accepts, total = Content-Length or unknown, reported at zero when the body starts and after each write; discarded bodies (a 3xx under `-L`, a 401/417 a retry answers) are not reported; upload counts are body bytes sent with the body length when known. A per-call `HttpTransferProgress` drops any count below one already reported, so a resent body never makes a total go back and "started" is never repeated on a reconnect.
- Default taken: the expected download total is the Content-Length alone, not offset by `-C`; nothing measured contradicts it and the console task that draws the totals can revisit it.
- Tests: `HttpProtocolHandlerTests.Progress.cs` (12, each over 1-byte and whole reads where the fake allows) and `HttpTransferProgressTests.cs` (3), with a recording sink in `Fakes/RecordingTransferProgress.cs`. `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary`: 100% line, 100% branch, 0 failing, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. The HTTP handler reports transfer start, Content-Length totals and running download/upload byte counts to the progress sink (ADR-0065)
