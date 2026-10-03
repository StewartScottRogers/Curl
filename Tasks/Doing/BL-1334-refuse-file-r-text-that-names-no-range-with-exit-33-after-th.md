---
id: BL-1334
title: Refuse file:// -r text that names no range with exit 33 after the open and the -i headers, as curl's file_do does
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests]
requirement: FR-007
created: 2026-10-03
completed:
---
# BL-1334 — Refuse file:// -r text that names no range with exit 33 after the open and the -i headers, as curl's file_do does

## Goal

`FileProtocolHandler` fails a download whose `ITransferContext.RangeText` names no range (`RangeText` set, `Range` `null`) with exit 33 `Requested range was not delivered by the server` at the point curl 8.21.0 does: after the file is opened and after the `-i` pseudo-headers are written, never under `-I`, and never ahead of an open failure.

## Context

- Today `Curl.Console/CurlCommandRunner.cs` `TryParseRange` refuses such text before any handler runs (exit 33, `ByteRangeParser.NotDeliveredFailure`), so the order is wrong in three measured cases. BL-1322 changes the console to hand the text to the file handler; this task makes the handler refuse it in curl's place first. `FileProtocolHandler` reads only `context.Range` today (`TryResolveWindow`, near line 1049).
- curl 8.21.0 `lib/file.c` (tag `curl-8_21_0`): the file is opened in `file_connect` (line 242, `Could not open file %s`, exit 37); `file_do` writes the `Content-Length`, `Accept-ranges: bytes` and `Last-Modified` header lines and the empty line (lines 425-468), returns `CURLE_OK` at once when `data->req.no_body` (`-I`), and only then calls `Curl_range(data)` (line 476), which returns `CURLE_RANGE_ERROR` (`lib/curl_range.c` lines 35-89) for `5-2`, `abc` and `-0`.
- Measured 2026-10-03 with the installed curl 8.21.0 (mingw, Schannel) against a 12-byte file `hello world\n`:
  - `-s -i -r 5-2 file:///.../f.txt`: stdout `Content-Length: 12\r\nAccept-ranges: bytes\r\nLast-Modified: <date> GMT\r\n\r\n`, exit 33.
  - `-sv -I -r 5-2 file:///.../f.txt`: the same header block, exit 0.
  - `-v -r 5-2 file:///.../nonexist.txt`: `* Could not open file ...`, `curl: (37) Could not open file ...`, exit 37.
  - `-sv -r 5-2 file:///.../f.txt`: stdout empty, exit 33 (the console's progress meter and `* shutting down connection #0` line are BL-1322's).
- `Range == null && RangeText != null` means exactly "names no range" for a non-HTTP scheme (see the remark on `ITransferContext.Range`); the handler needs no parser. An upload (`-T`) ignores `-r` as today.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.File.UnitTests` runs a download of a temporary file with `RangeText = "5-2"`, `Range = null` and asserts exit 33 (`CurlExitCode.RangeError`) with message `Requested range was not delivered by the server` and nothing written to the output.
- [ ] A test with `ITransferContext.HeaderOutput` set, as the existing `-i` tests set it, asserts the three pseudo-header lines and the empty line are written before the exit 33 result.
- [ ] A test with `NoBody = true` asserts exit 0 and the header block, as for a download with no `-r`.
- [ ] A test for a file that does not exist asserts exit 37 with the existing `Could not open file` message, not exit 33.
- [ ] A data-driven test pins exit 33 for `abc` and `-0`; existing range tests (`Range` set) pass unchanged.
- [ ] Every test uses a temporary file path built with `Path.GetTempPath()`, so it passes on Windows, Linux and macOS.
- [ ] `dotnet build Curl.Protocol.File.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.File.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.File.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
