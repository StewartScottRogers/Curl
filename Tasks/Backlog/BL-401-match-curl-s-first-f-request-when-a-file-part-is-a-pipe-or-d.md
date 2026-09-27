---
id: BL-401
title: Match curl's first -F request when a file part is a pipe or device
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-401 — Match curl's first -F request when a file part is a pipe or device

## Goal

`curl -F f=@\\.\pipe\<name> <url>` sends the same first request as curl 8.21.0 (Schannel build): the same `Content-Length`, framing and body bytes, or a recorded decision not to match.

## Context

- Found in BL-359 (2026-09-27). With a named pipe delivering `hello`, curl 8.21.0 sent `Content-Length: 209` (the size a 0-byte file gives, as `stat` of a pipe reports 0) and then a body cut off at that length, `...\r\n\r\nhello\r\n--<boundary>` without the closing `--\r\n`. Our `MultipartFormBodyBuilder` (`Curl.Core.UnitLibrary/Multipart`) sends a non-seekable file part chunked with no length instead - unmeasured difference.
- Measure with `Record-CurlExchange.ps1` and a `System.IO.Pipes.NamedPipeServerStream` started in a `Start-Job` that writes the file bytes and closes (the BL-359 Notes give the command). Also measure a pipe that delivers nothing, and one whose data exceeds the declared length.
- Matching a truncated body is a drop-in question: decide by the standing rules and record an ADR if it is a choice rather than a measurement.

## Acceptance criteria

- [ ] The measured first request for a pipe file part (5 bytes, and 0 bytes) is recorded in Notes with the command.
- [ ] A `MultipartFormBodyBuilderTests` or `CurlCommandRunnerFormTests` case pins our first request against those bytes, or an ADR records why it differs.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every library touched.

## Notes

## Log

- 2026-09-27: Created.
