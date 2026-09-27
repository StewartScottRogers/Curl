---
id: BL-328
title: Resume an HTTP -T upload from -C with the measured Content-Range
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-184]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-328 — Resume an HTTP -T upload from -C with the measured Content-Range

## Goal

An HTTP `-T` upload with `-C <offset>` (or `-C -`) skips the offset and sends the request curl 8.21.0 sends for it, measured.

## Context

- BL-184 sends `ITransferContext.Upload` as PUT from the stream's current position and ignores `ITransferContext.ResumeFrom` (ADR-0052, Consequences).
- Measure first with `/mingw64/bin/curl` (curl 8.21.0, the Windows reference, ADR-0009) against a loopback server: `-C 3 -T f.txt`, and `-C - -T f.txt`, which asks the server with a HEAD for the size it has. Record the commands and bytes in Notes before pinning them.
- Start in `HttpRequestFraming.OfUpload` and `HttpRequestHeadFormatter`.
- Filed from BL-184.

## Acceptance criteria

- [ ] `-C 3 -T f.txt` over HTTP sends the request bytes curl 8.21.0 sent (measured), pinned in a `HttpProtocolHandlerTests` test.
- [ ] `-C - -T f.txt` sends the measured request sequence, or the Notes record the measured behaviour and a follow-up task for it.
- [ ] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes`, never a socket.

## Notes

## Log

- 2026-09-27: Created.
