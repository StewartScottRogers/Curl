---
id: BL-332
title: Resume an HTTP -T upload from -C with the measured Content-Range
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-184]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-332 — Resume an HTTP -T upload from -C with the measured Content-Range

## Goal

An HTTP `-T` upload with `-C <offset>` (or `-C -`) skips the offset and sends the request curl 8.21.0 sends for it, measured.

## Context

- BL-184 sends `ITransferContext.Upload` as PUT from the stream's current position and ignores `ITransferContext.ResumeFrom` (ADR-0055, Consequences).
- Measure first with `/mingw64/bin/curl` (curl 8.21.0, the Windows reference, ADR-0009) against a loopback server: `-C 3 -T f.txt`, and `-C - -T f.txt`, which asks the server with a HEAD for the size it has. Record the commands and bytes in Notes before pinning them.
- Start in `HttpRequestFraming.OfUpload` and `HttpRequestHeadFormatter`.
- Filed from BL-184.

## Acceptance criteria

- [x] `-C 3 -T f.txt` over HTTP sends the request bytes curl 8.21.0 sent (measured), pinned in a `HttpProtocolHandlerTests` test.
- [x] `-C - -T f.txt` sends the measured request sequence, or the Notes record the measured behaviour and a follow-up task for it.
- [x] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes`, never a socket.

## Notes

### Measured (2026-09-27, `/mingw64/bin/curl` 8.21.0 Schannel, loopback Python server answering 200)

f.txt = `abcdefghij` (10 bytes); `
` shown as written.

- `curl -s -C 3 -T f.txt http://127.0.0.1:18332/up` -> exit 0, sent
  `PUT /up HTTP/1.1
Host: 127.0.0.1:18332
Content-Range: bytes 3-9/10
User-Agent: curl/8.21.0
Accept: */*
Content-Length: 7

defghij`
- `curl -s -C - -T f.txt http://127.0.0.1:18333/up` -> exit 0, **no HEAD**, sent
  `PUT /up HTTP/1.1
Host: 127.0.0.1:18333
Content-Range: bytes 0-9/10
User-Agent: curl/8.21.0
Accept: */*
Content-Length: 10

abcdefghij`
- `curl -s -C 0 -T f.txt .../up` (18334) -> exit 0, no Content-Range, `Content-Length: 10`, whole file.
- `curl -sS -C 20 -T f.txt` (18335) and `-C 10` (18336) -> exit 18 `curl: (18) File already completely uploaded`; connected, nothing sent.
- `curl -sS -C 3 -T f.txt -H "Content-Range: bytes 9-9/10"` (18338) -> exit 0; curl's own Content-Range left out, the `-H` line after `Accept`, `Content-Length: 7`, body `defghij`.
- `curl -sS -C 3 -T empty.txt` (18339, 0-byte file) -> exit 26 `curl: (26) Unable to resume from offset 3`; connected, nothing sent.
- `cat f.txt | curl -sS -C 3 -T -` (18337) -> exit 0, sent
  `PUT /up HTTP/1.1
Host: 127.0.0.1:18337
Content-Range: bytes 3-1/2
User-Agent: curl/8.21.0
Accept: */*
Transfer-Encoding: chunked
Expect: 100-continue

a
abcdefghij
0

`
  (unknown length counted as -1: total = -1 + 3 = 2; nothing skipped).

### Decisions (ADR-0057, decided by Claude under Stewart's delegation)

- New `HttpUploadResume` applies the offset: seeks a seekable source past it, formats
  `Content-Range: bytes N-(T-1)/T` with T = bytes left + N (left = -1 when unknown), and carries
  the exit 18 / exit 26 refusals, thrown by `HttpProtocolHandler.ThrowIfRefused` after connect.
  `HttpRequestFraming` holds it; `HttpRequestHeadFormatter` puts `Content-Range` in the `Range` slot.
- Standard input is sent whole with the measured `bytes 3-1/2`: the Windows reference build's
  behaviour, which is the platform rule.
- `-C -` with `-T`: the handler gets `ResumeFrom` `null` (Console resolves `-C -` only against an
  output file), so it cannot tell it from no `-C`; that needs a contract change in
  `Curl.Protocol.Abstractions.UnitLibrary` (held by BL-335) and `Curl.Console`. Filed as **BL-351**;
  the measured bytes are above. Acceptance criterion 2 is met by recording them and the follow-up.
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0057 and its README row; no task in
  Doing names it.

### Gates

- `dotnet build -warnaserror`: clean. Fast tests: all green (Http 751, 6 new).
- `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10.
- `dotnet format --verify-no-changes` on both projects: clean.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. HTTP -T with -C N skips N bytes and sends curl 8.21.0's measured Content-Range; exit 18/26 refusals match; -C - filed as BL-351
