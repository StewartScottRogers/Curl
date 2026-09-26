---
id: BL-175
title: Send HTTP request bodies from BytesBody and StreamBody with curl's Expect: 100-continue
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-173]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-175 — Send HTTP request bodies from BytesBody and StreamBody with curl's Expect: 100-continue

## Goal

The handler sends `BytesBody` and `StreamBody` request bodies with Content-Length and Content-Type as curl does, and handles `Expect: 100-continue` with curl's threshold and wait.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item H7. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Measured `curl -d x=1 http://127.0.0.1:18081/`: `POST / HTTP/1.1\r\nHost: 127.0.0.1:18081\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nContent-Length: 3\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\nx=1`.
- `--json` sets its own Content-Type and Accept (measure). curl sends `Expect: 100-continue` above a size threshold and waits a fixed time for 100 (measure both on curl 8.21.0).
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] `-d x=1` equivalent (`BytesBody`, form content type) is byte-equal to the measured POST.
- [x] A `--json` body sends the measured Content-Type and Accept headers.
- [x] The Expect threshold and wait are measured and pinned; the wait runs on `FakeTimeProvider`.
- [x] A failed `StreamBody` read returns `CurlExitCode.ReadError` (26) with the measured message.
- [x] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes` (added by BL-169), never a socket; every parser test also runs with 1-byte chunks.

## Notes

- Plan item: H7 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Delivered: `HttpRequestFraming` (method, chunking, Expect and wait), `HttpRequestHeadFormatter` body headers, `HttpRequestBodyWriter` (bytes, streams, chunk framing, exit 26), `HttpContinueWaitConnection` (1-second wait for 100 Continue, replays what it read), wired into `HttpProtocolHandler`. The test fakes gained timers on `FakeTimeProvider`, `GatedConnection` and `FailingReadStream`.
- Measured on curl 8.21.0 (mingw, `/mingw64/bin/curl`) against loopback servers on 127.0.0.1 (Python socket servers, because the wait timing and the early replies need a server that holds back or answers before the body; `Record-CurlExchange.ps1` for the first two):
  - `curl -d x=1 http://127.0.0.1:18081/` sent `POST / HTTP/1.1
Host: 127.0.0.1:18081
User-Agent: curl/8.21.0
Accept: */*
Content-Length: 3
Content-Type: application/x-www-form-urlencoded

x=1`; `-w '%{size_request} %{size_upload} %{method}'` printed `151 3 POST`.
  - `curl --json {"a":1} http://127.0.0.1:18081/` sent `...User-Agent: curl/8.21.0
Content-Type: application/json
Accept: application/json
Content-Length: 7

{"a":1}`. With `-H 'X-A: b'` before or after `--json`, `X-A: b` came first: --json's two headers follow every -H. With `-H 'Accept: foo'`, only `Content-Type: application/json` was added.
  - `-d x=1 -H 'X-A: b'`: `X-A: b` came before `Content-Length`. `-H 'Content-Type: text/plain'` replaced curl's in the -H slot. `-H 'Content-Type:' -H 'Content-Length: 3'`: no Content-Type, the -H Content-Length in its slot and none of curl's. `-d ''`: `Content-Length: 0` and the form Content-Type.
  - `--data-binary @a` (1048576 bytes): no Expect, body at once. `@b` (1048577 bytes): `Expect: 100-continue` after Content-Type; with no reply the body's first byte arrived 1.005 s after the head; with an immediate `100 Continue` it came at once. `-w` printed `1048753 1048577 POST`.
  - `-H 'Expect:'` with 1048577 bytes: no Expect, no wait. `-H 'Expect: 100-continue' -d x=1`: the header in its -H slot and a 1.01 s wait.
  - `-X POST -T -` with `hello world` on a pipe: `Transfer-Encoding: chunked
Expect: 100-continue

`, then after 1.007 s `b
hello world
0

`. `-d x=1 -H 'Transfer-Encoding: chunked'`: the -H line, then the form Content-Type, no Content-Length, no Expect, body `3
x=1
0

`.
  - A server answering `HTTP/1.1 401 No` (Content-Length 3, `no!`) as soon as the Expect head arrived: curl sent no body byte, printed `no!`, exit 0. A 417 reply: curl resent the head without Expect plus the body (BL-260).
  - `-X PUT -d x=1`: `-w` printed `150 3 PUT`.
  - `curl -F f=@locked`, a 100000-byte file with every byte locked by another process (`msvcrt.locking`) so each read fails: `Content-Length: 100207` sent, then only the 207 multipart framing bytes; exit 26, `curl: (26) client mime read EOF fail, only 207/100207 of needed bytes read`. curl's reader takes the failed read as end of file.
- Decisions (Claude, under Stewart's delegation; the ADR is BL-258, because `Documentation/Planning/Decisions` is in the `touches` of BL-154, in Doing on another lane):
  - `--json` reaches the handler as a `BytesBody` with `application/json` plus `Content-Type: application/json` and `Accept: application/json` appended after every -H, each only when no -H names it. That is the measured order; the handler then needs no --json flag. Filling the options is BL-231.
  - A StreamBody of unknown length that fails a read ends the chunked body there, as curl's reader does; one of known length fails with exit 26 and the measured message (a stream that simply ends early gets the same, as in curl).
  - A final status that arrives during the wait leaves the body unsent and is the response (measured with 401). The 417 retry is filed as BL-260.
  - `TransferReport.RequestSize` counts head and body, as `%{size_request}` does; its doc comment in Abstractions still says "body excluded", filed as BL-259.
  - The wait reads only up to the first line feed to decide 100 vs final; the buffer is capped at the 100 KiB line limit so a server that never sends a line feed cannot grow it.
- Follow-ups filed: BL-258 (ADR), BL-259 (RequestSize doc), BL-260 (417 retry).
- Gates: `dotnet build -warnaserror` clean; fast tests green solution-wide (Http 374); `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http*` reports 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. HttpProtocolHandler sends BytesBody and StreamBody bodies with curl's Content-Length, chunking, Content-Type and 1-second Expect: 100-continue wait; failed body reads return exit 26
