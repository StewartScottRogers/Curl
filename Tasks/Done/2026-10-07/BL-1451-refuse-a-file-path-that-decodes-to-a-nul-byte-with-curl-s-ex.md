---
id: BL-1451
title: Refuse a file:// path that decodes to a NUL byte with curl's exit 3 before opening anything
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests]
requirement: FR-019
created: 2026-10-04
completed: 2026-10-07
---
# BL-1451 — Refuse a file:// path that decodes to a NUL byte with curl's exit 3 before opening anything

## Goal

A `file://` URL whose path percent-decodes to a NUL byte (`%00`) fails before anything is opened, for a download and an upload alike, with curl 8.21.0's `curl: (3) URL using bad/illegal format or missing URL`, instead of Curl's exit 37 `Could not open file ...%00x` (download) or exit 23 `cannot open <path with the NUL turned into a space> for writing` (upload).

## Context

- Upstream (tag `curl-8_21_0`), `lib/file.c` lines 162-165 (`file_connect`): the path is decoded with `Curl_urldecode(..., REJECT_ZERO)`, which fails with `CURLE_URL_MALFORMAT` when a `%00` decodes to a zero byte; no `failf` is written, so the tool prints the bare `curl_easy_strerror` text, `URL using bad/illegal format or missing URL`. Other control characters (`%0a`, for instance) are not refused: real curl tries to open the file and fails with exit 37 `Could not open file .../f%0Atxt`, as Curl already does.
- Measured on Windows with real curl 8.21.0 and `Curl.Console` on 2026-10-04: `curl -sS "file:///<dir>/f.txt%00x"` exits 3 with the line above in curl and 37 in Curl; `curl -sS -T dd/f.txt "file:///<dir>/up%00x"` exits 3 in curl and 23 in Curl (`cannot open ...\up x for writing`). Measure `-v` too before pinning: whether curl writes any `*` line before the error.
- Curl today: `Curl.Protocol.File.UnitLibrary/FileUrlPath.cs` `Decode` (line 270) decodes every escape, NUL included; `FileProtocolHandler.TransferAsync` already reports `CurlExitCode.UrlMalformat` for a URL `FileUrlPath.TryParse` refuses (with the `URL rejected: Bad file:// URL` text, which is not the one wanted here).
- Use drive-less URLs (`file:///dir/f%00x`) in tests so they pass on Windows, Linux and macOS; the refusal happens before the path reaches the file system on every platform.

## Acceptance criteria

- [x] Tests in `Curl.Protocol.File.UnitTests` pin a download of `file:///dir/f.txt%00x` and an upload to `file:///dir/up%00x` each failing with `CurlExitCode.UrlMalformat` and the message `URL using bad/illegal format or missing URL`, with nothing opened or created (assert on the fake file system), and the measured `-v` lines.
- [x] A test pins that `file:///dir/f%0atxt` is still decoded and opened (no refusal), as today.
- [x] `dotnet build Curl.Protocol.File.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.File.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.File.UnitLibrary` reports no failing member.

## Notes

- Measured 2026-10-07 on Windows, curl 8.21.0 (Schannel and MinGW builds): `curl -sSv file:///dir/f.txt%00x` and `curl -sSv -T f.txt file:///dir/up%00x` write only `curl: (3) URL using bad/illegal format or missing URL` - no `*` line under `-v`, not even `shutting down connection` - and create nothing. `file:///dir/f%0atxt` still exits 37 `Could not open file /dir/f%0Atxt`.
- `FileProtocolHandler.TransferAsync` now refuses a parsed path whose `OsPath` holds a NUL with `CurlExitCode.UrlMalformat` and `FileTransferMessages.UrlMalformed`, before the resume check and any `IFileSystem` call, without `ReportFailure` (so no info line). ADR-0416.
- Pinned in `FileProtocolHandlerNulBytePathTests` (download, upload, `%0a` still opened); the `-v` lines measured are none, asserted as an empty transcript.
- Found: a Windows drive-letter *download* (`file:///C:/dir/f.txt%00x`) gets `* URL rejected: Malformed input to a URL function` from curl's URL parser instead; that is URL parsing in Curl.Protocol.Abstractions, outside this task's touches, so it is filed as BL-1526.
- Measure-CodeQuality.ps1 -Library Curl.Protocol.File.UnitLibrary: 0 failing members.

## Log

- 2026-10-04: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. A file:// path decoding to NUL now fails with exit 3 'URL using bad/illegal format or missing URL' before anything opens, download and upload
