---
id: BL-184
title: Upload -T with PUT, with a known length or chunked from stdin
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-175, BL-030]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-184 — Upload -T with PUT, with a known length or chunked from stdin

## Goal

An upload source on `ITransferContext.Upload` is sent as PUT with Content-Length when the length is known and `Transfer-Encoding: chunked` when it is not.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item H16. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-030 wires the `-T` URL into the dispatcher.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] A file upload sends PUT with Content-Length byte-equal to curl 8.21.0 (measured).
- [x] A non-seekable (stdin) upload sends `Transfer-Encoding: chunked` byte-equal to curl (measured).
- [x] A read failure returns `CurlExitCode.ReadError` (26) with the measured message.
- [x] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes` (added by BL-169), never a socket; every parser test also runs with 1-byte chunks.

## Notes

- Plan item: H16 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Measured 2026-09-27 with `/mingw64/bin/curl` (curl 8.21.0 x86_64-w64-mingw32, Schannel) against a Python loopback server on 127.0.0.1 that read the whole request (1.5 s idle timeout), answered `HTTP/1.1 200 OK
Content-Length: 0

` and closed. `Record-CurlExchange.ps1` was not used because it cannot feed curl's standard input. Files were locked with `msvcrt.locking` from another process.
  - `curl -sS -T f.txt http://127.0.0.1:18184/u` (`hello`): `PUT /u HTTP/1.1
Host: 127.0.0.1:18184
User-Agent: curl/8.21.0
Accept: */*
Content-Length: 5

hello`, exit 0. No Content-Type.
  - `printf hello | curl -sS -T - http://127.0.0.1:18185/u`: `PUT /u HTTP/1.1
Host: 127.0.0.1:18185
User-Agent: curl/8.21.0
Accept: */*
Transfer-Encoding: chunked
Expect: 100-continue

5
hello
0

`, exit 0.
  - 200000 bytes piped to `curl -T - http://127.0.0.1:18190/u`: chunks 65524, 65524, 65524, 3428. With `-H "Expect:"` (port 18191, 108-byte head): chunks 65416, 65524, 65524, 3536. So the head shares the 64 KiB buffer with the first read unless it went out alone for the 100-continue wait, and each chunked read keeps 12 bytes back.
  - `curl -sS -T big.bin http://127.0.0.1:1818x/u`, 100000 bytes: all locked -> nothing sent, `curl: (26) client read function EOF fail, only 0/100000 of needed bytes read`; locked from 70000 -> the 104-byte head and 65432 bytes, `only 65432/100000`; 200000 bytes locked from 140000 -> `only 130968/200000` (65432 + 65536).
  - `-T big2.bin` (2000000 bytes): `Content-Length: 2000000` then `Expect: 100-continue`, as the existing framing gives.
- Delivered: `HttpRequestFraming.Of` takes `ITransferContext.Upload` (PUT unless `-X`, length left from the position when seekable, else chunked; `IsUpload`); the head formatter sends no Content-Type for an upload; `HttpRequestBodyWriter` reads in curl's 64 KiB buffer (`SharedHeadLength`, `ChunkFramingReserve`) and reports an upload's short read with `client read function EOF fail`; the handler wires both. `FailingReadStream` gained an optional length that makes it seekable. Tests: `HttpProtocolHandlerTests.Upload.cs` (byte-exact PUT, stdin chunked with the 1 s wait, the measured chunk sizes with and without Expect, exit 26), plus framing and writer tests.
- Decisions (Claude, under Stewart's delegation): ADR-0052. An upload takes the place of a `-d`/`-F` body (curl's command line refuses both together). The buffer arithmetic applies to `-F` stream bodies too, since curl reads them through the same buffer. A head of 64 KiB or more gives the first read a whole buffer (unmeasured).
- Touches: added `Documentation/Planning/Decisions` for ADR-0052; no task in Doing names it.
- Differences left: curl sends nothing when the very first read fails, the handler sends the head (BL-329). `-C` with `-T` over HTTP is not handled (BL-328).
- Gates: `dotnet build` clean; `dotnet test --filter "TestCategory!=Integration"` green (Curl.Protocol.Http.UnitTests 725 passed); `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary`: 100% line, 100% branch, 279 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. An HTTP -T upload is sent as PUT: Content-Length for a file, chunked for stdin, read in curl's 64 KiB buffer, exit 26 with the measured client read function message
