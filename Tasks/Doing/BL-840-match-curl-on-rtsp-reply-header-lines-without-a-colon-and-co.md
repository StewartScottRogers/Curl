---
id: BL-840
title: Match curl on RTSP reply header lines without a colon and Content-Length lists
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-591]
touches: [Curl.Protocol.Rtsp.UnitLibrary, Curl.Protocol.Rtsp.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-840 — Match curl on RTSP reply header lines without a colon and Content-Length lists

## Goal

`RtspReplyReader` treats the reply header lines BL-591 did not measure as curl 8.21.0 does: a header line with no colon, a carriage return inside a line, a folded continuation line, and a `Content-Length` given as a comma list (`2, 2`) or twice.

## Context

- BL-591 built `RtspReplyHeadParser` and `RtspReplyReader` (ADR-0169). It reads `CSeq` and `Content-Length` only; any other line is accepted and written, and a `Content-Length` that is not one decimal number fails with 8, `Invalid Content-Length: value`.
- Over HTTP curl fails such lines with 8 (`Header without colon`, `Carriage return found in header`) and accepts a list of equal numbers (`Curl.Protocol.Http.UnitLibrary`'s `HttpTransferMessages`). Whether RTSP does the same is unmeasured.
- Measure each case with `Record-CurlExchange.ps1` against Git for Windows' mingw64 curl 8.21.0 (Windows' own `curl.exe` has no `rtsp`), `-sS -i rtsp://127.0.0.1:<port>/media`.

## Acceptance criteria

- [ ] Each measurement (reply, stdout, stderr, exit) copied into Notes.
- [ ] `Curl.Protocol.Rtsp.UnitTests` pin the output and outcome of each measured reply through a fake connection.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Rtsp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
