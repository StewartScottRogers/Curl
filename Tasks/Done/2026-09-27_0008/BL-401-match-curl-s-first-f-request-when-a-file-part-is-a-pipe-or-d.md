---
id: BL-401
title: Match curl's first -F request when a file part is a pipe or device
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-401 — Match curl's first -F request when a file part is a pipe or device

## Goal

`curl -F f=@\\.\pipe\<name> <url>` sends the same first request as curl 8.21.0 (Schannel build): the same `Content-Length`, framing and body bytes, or a recorded decision not to match.

## Context

- Found in BL-359 (2026-09-27). With a named pipe delivering `hello`, curl 8.21.0 sent `Content-Length: 209` (the size a 0-byte file gives, as `stat` of a pipe reports 0) and then a body cut off at that length, `...\r\n\r\nhello\r\n--<boundary>` without the closing `--\r\n`. Our `MultipartFormBodyBuilder` (`Curl.Core.UnitLibrary/Multipart`) sends a non-seekable file part chunked with no length instead - unmeasured difference.
- Measure with `Record-CurlExchange.ps1` and a `System.IO.Pipes.NamedPipeServerStream` started in a `Start-Job` that writes the file bytes and closes (the BL-359 Notes give the command). Also measure a pipe that delivers nothing, and one whose data exceeds the declared length.
- Matching a truncated body is a drop-in question: decide by the standing rules and record an ADR if it is a choice rather than a measurement.

## Acceptance criteria

- [x] The measured first request for a pipe file part (5 bytes, and 0 bytes) is recorded in Notes with the command.
- [x] A `MultipartFormBodyBuilderTests` or `CurlCommandRunnerFormTests` case pins our first request against those bytes, or an ADR records why it differs.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every library touched.

## Notes

- Measured 2026-09-27, curl 8.21.0 (Git for Windows `mingw64\bin\curl.exe`, Schannel). The pipe was a
  `NamedPipeServerStream('<name>', Out)` in a `Start-Job` that waited for the connection, wrote N bytes
  (`abcde...`), flushed, waited 300 ms and closed; the script waited until `\\.\pipe\` listed the pipe
  before running
  `Record-CurlExchange.ps1 -Port <p> -Response 'HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n' -CurlArgs '-sS','-F','f=@\\.\pipe\<name>','http://127.0.0.1:<p>/first'`.
  - 5 bytes (name `bl401m-18412-5`, boundary `------------------------1PQGfpXEKWOBJUw2cVmmCb`): exit 0,
    `Content-Length: 216`, body of 216 bytes: the part headers, `abcde`, CRLF, `--<boundary>`, and
    no closing `--` CRLF.
  - 0 bytes (name `bl401m-18413-0`, boundary `...hPRLUVz4YTDnuhseH9gMXT`): exit 0, `Content-Length: 216`,
    215 body bytes (the whole 0-byte-file body), then curl waited; the recorder's 1 s read timeout
    answered it. With the server holding the pipe open 3 s instead, curl had sent nothing when the
    recorder gave up (exit 56). An earlier orphaned run of this task recorded exit 26
    `read error getting mime data` for 0 and 500 bytes: the 0-byte outcome depends on pipe timing.
  - 500 bytes: exit 0, `Content-Length: 218` (name 2 characters longer), body cut the same way.
  - `-F f=@NUL`: exit 0, `Content-Length: 204`, the whole 0-byte-file body, not chunked.
  - The "1 byte" a one-instance pipe declares is its instance count: with 3 server instances and 3 bytes
    delivered, `Content-Length` grew by 2 and the body was whole. `DirectoryInfo('\\.\pipe\')`
    enumeration reports the same count as the entry's `Length` (1 and 3).
- libcurl 8.21.0 `lib/mime.c` `curl_mime_filedata`: `datasize = -1` (chunked) unless `S_ISREG`; on Windows
  `curlx_stat` is `_wstati64`, which calls pipes and `NUL` regular. So Windows declares a length, and
  Linux and macOS keep the chunked body we already sent.
- Decision (ADR-0097): new `UnseekableFileLength` (Core, Multipart) - Windows: local pipe -> instance count
  from `\\.\pipe\` via `FileSystemEnumerable`, anything else -> 0; elsewhere -> null (chunked).
  `MultipartFormBodyBuilder` takes it as a new optional last constructor argument defaulting to
  `ForPlatform(OperatingSystem.IsWindows())`, so `Curl.Console` (held by another lane) needs no change.
  No truncation in Core: `HttpRequestBodyWriter` already stops at `StreamBody.Length`. Not matched: a pipe
  delivering less than it declared fails exit 26 where curl waits for the server.
- Pinned in `MultipartFormBodyBuilderTests.APipeFileDeclaresTheLengthWindowsStatGivesItAndItsFirstBytesMatchCurl`
  (5 and 0 bytes, measured boundaries and names) and `TheNullDeviceDeclaresNoBytesOnWindowsAsCurlSendsIt`
  (204); `UnseekableFileLengthTests` covers the rule, with real pipes under `[OSCondition(Windows)]`. The
  builder test helpers now pass `UnseekableFileLength.Unknown` explicitly so the existing chunked tests stay
  platform-neutral.
- Touches: added `Documentation/Planning/Decisions` for ADR-0097 and its index line; no task in Doing
  names it.
- Filed BL-445: `/dev/null` on Linux and macOS opens seekable (length 0) so we declare a length where libcurl
  sends a character device chunked; depends on BL-402's measurement.
- `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary`: 100% line, 100% branch, 0 failing, worst CRAP 10.
  Pipeline stages run in-session (no subagents) for this one-class change.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Backlog. Orphaned by a stopped shift: no lane worktree or branch held its work; requeued.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -F with a Windows named pipe or NUL declares curl's stat length (pipe instance count, NUL 0) and is cut off there, as curl 8.21.0 sends it
