---
id: BL-6056
title: Remove the unused FTP stand-in num_headers line from TransferReport and the engine
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-6019]
touches: [Curl.Protocol.Abstractions.UnitLibrary/TransferReport.cs, Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.ProxyConnect.cs, Curl.Engine.UnitTests/Std222Tests.cs, Documentation/Planning/Decisions/ADR-1434-a-connect-reply-counts-in-size-header-and-num-headers-for-every-protocol.md]
requirement: none
created: 2026-11-26
completed: 2026-11-27
---
# BL-6056 — Remove the unused FTP stand-in num_headers line from TransferReport and the engine

## Goal

`TransferReport.TransferCompleteStandInHeader` and the engine code that removes it are gone, and no
document says FTP reports a stand-in header line.

## Context

BL-6020 (STD-223) measured that curl 8.22.0 counts no stand-in line: FTP's `%{num_headers}` is the
reply lines holding a colon plus the `-I` lines, so `FtpSession` no longer reports one and nothing
produces `TransferCompleteStandInHeader`. What is left is dead:

- `Curl.Protocol.Abstractions.UnitLibrary/TransferReport.cs`: the property and its doc comment.
- `Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.ProxyConnect.cs`: `WithConnectReplies` removes
  the stand-in from the pseudo headers.
- `Curl.Engine.UnitTests/Std222Tests.cs`: two tests build a report holding it.
- ADR-1434 says the stand-in is shared with the engine; add a dated amendment.

## Acceptance criteria

- [x] No `.cs` file names `TransferCompleteStandInHeader`.
- [x] `Std222Tests` still pins the CONNECT reply counts for a transfer with no headers of its own and
      for one with a reply line holding a colon.
- [x] ADR-1434 carries a dated amendment saying the stand-in was removed.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Removed `TransferReport.TransferCompleteStandInHeader` and the removal of it in `WithConnectReplies`; ADR-1434 amended.
- `Std222Tests`: the two tests that built a report holding it now use no headers of its own and a reply line holding a colon (`ReportOn_TwoTunnelsAndNoHeadersOfItsOwn_CountsTheLastReplyAlone`, `ReportOn_AReplyWithNoHeaderLinesAndAGreetingLine_CountsTheLineAlone`).
- Verified: build 0 warnings; fast tests 58932 passed, 0 failed, 363 skipped; format check clean on the changed files; Measure-CodeQuality for both libraries 0 failing members.

## Log

- 2026-11-26: Created.
- 2026-11-27: Backlog -> Doing. Criteria at claim: 4b036c2d 2613a0f7 1a6b2dbc 3d836d5e.
- 2026-11-27: Doing -> Done. The unused FTP stand-in num_headers line is gone from TransferReport and the engine
