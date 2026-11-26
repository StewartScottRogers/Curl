---
id: BL-6054
title: Remove the unused FTP stand-in num_headers line from TransferReport and the engine
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-6019]
touches: [Curl.Protocol.Abstractions.UnitLibrary/TransferReport.cs, Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.ProxyConnect.cs, Curl.Engine.UnitTests/Std222Tests.cs, Documentation/Planning/Decisions/ADR-1434-a-connect-reply-counts-in-size-header-and-num-headers-for-every-protocol.md]
requirement: none
created: 2026-11-26
completed:
---
# BL-6054 — Remove the unused FTP stand-in num_headers line from TransferReport and the engine

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

- [ ] No `.cs` file names `TransferCompleteStandInHeader`.
- [ ] `Std222Tests` still pins the CONNECT reply counts for a transfer with no headers of its own and
      for one with a reply line holding a colon.
- [ ] ADR-1434 carries a dated amendment saying the stand-in was removed.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created.
