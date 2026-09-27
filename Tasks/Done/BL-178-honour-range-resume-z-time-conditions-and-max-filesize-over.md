---
id: BL-178
title: Honour Range, resume, -z time conditions and --max-filesize over HTTP
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-173]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-178 — Honour Range, resume, -z time conditions and --max-filesize over HTTP

## Goal

The HTTP handler sends Range for `ByteRange`/`ResumeFrom`, If-Modified-Since/If-Unmodified-Since for `TimeCondition`, and enforces `MaxFileSize`, with curl's exits.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item H10. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- `ITransferContext.Range`, `ResumeFrom`, `MaxFileSize` and `TimeCondition` already exist; `-r`, `-C` and `--max-filesize` reach them (BL-095); `-z` parsing is BL-138.
- Measured: `-C` against a server that ignores ranges gives exit 33 `curl: (33) HTTP server does not seem to support byte ranges. Cannot resume.`
- `-R`/`--remote-time` (BL-079) stamps the output from `TransferResult.SourceLastWriteTimeUtc`; the HTTP source time is the `Last-Modified` header.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] Range from `ByteRange` and from `ResumeFrom` is sent in curl's form (measured); a 200 answer to a resume returns `CurlExitCode.RangeError` (33) with the measured message.
- [x] `TimeCondition` sends If-Modified-Since or If-Unmodified-Since in RFC 1123 form (measured); a 304 writes no body and exits 0.
- [x] Content-Length over `MaxFileSize` returns `FilesizeExceeded` (63) with the measured message.
- [x] `TransferResult.SourceLastWriteTimeUtc` is set from `Last-Modified` when present.
- [x] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes` (added by BL-169), never a socket; every parser test also runs with 1-byte chunks.

## Notes

- Plan item: H10 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0041 and its index row. No task in Doing names it.
- Delivered in this session without a separate architect pass. The seams already existed (`ITransferContext` members, `TransferResult.TimeConditionNotMet`), so the change stays inside `Curl.Protocol.Http.UnitLibrary`: `HttpRangeHeader`, `HttpDownloadConditions`, `HttpBodyDelivery`, `HttpContentRange`, `HttpLastModified`, plus a body-size limit on `HttpResponseBodyReader`.
- Measured 2026-09-26 with curl 8.21.0 (`/mingw64/bin/curl`) through `Record-CurlExchange.ps1`, `-sS` on every run, URL `http://127.0.0.1:<port>/f`. Request heads are `GET /f HTTP/1.1`, `Host`, then as listed:
  - `-r 0-99` / `-r 100-` / `-r -500`: `Range: bytes=0-99` / `bytes=100-` / `bytes=-500`, between `Host` and `User-Agent`. A 200 answer writes the whole body, exit 0.
  - `-u u:p -e ref --compressed -b a=b -H "X-A: 1" -r 0-9 -z "Sun, 06 Nov 1994 08:49:37 GMT"`: `Host`, `Authorization: Basic dTpw`, `Range: bytes=0-9`, `User-Agent`, `Accept`, `Accept-Encoding`, `Referer: ref`, `Cookie: a=b`, `If-Modified-Since: Sun, 06 Nov 1994 08:49:37 GMT`, `X-A: 1`.
  - `-H "Range: bytes=1-2" -r 0-9` sends only the `-H` line, in the `-H` slot. `-H "If-Modified-Since: x" -z ...` does the same.
  - `-d x -r 0-9 -z ...`: `POST` with `Content-Range: bytes 0-9/1` after `Host` and no `Range`. Curl sends neither (ADR-0041), filed as BL-300.
  - `-C 100`: `Range: bytes=100-`. A 206 with `Content-Range: bytes 100-104/105` writes `hello`, exit 0, `%{response_code} %{size_download}` = `206 5`. A 200 with `Content-Range: bytes 100-104/105` is accepted too.
  - `-C 100` answered by a 200 without Content-Range, a 206 from 50, a 206 with no Content-Range, a 404 or a 304: exit 33, stderr `curl: (33) HTTP server does not seem to support byte ranges. Cannot resume.`, no body. With `-i` the head is written first.
  - `-C 100` answered by a 416 (`Content-Range: bytes */100` or `*/50`, with or without a 4-byte body): exit 0, with `-i` the head is written and the body is not.
  - `-C 5` answered by a 200 with Content-Length 5: exit 0, no body. `-C 0`: no `Range`, body written. `-I -C 100`: `HEAD` with `Range: bytes=100-`, 200 accepted, exit 0.
  - `-C 100 -r 0-5`: exit 2 at the command line. `-C 100 -d x`: exit 2. The handler never sees these combinations.
  - `-z "<date>"`: `If-Modified-Since: <date>`. `-z "-<date>"`: `If-Unmodified-Since: <date>`, placed after `Accept`. A 304 writes no body, exit 0 (with `-i`, `HTTP/1.1 304 Not Modified

`). A 412 for `-z -date` writes its body, exit 0.
  - `-z date` answered by a 200 with `Last-Modified` older or equal: no body, exit 0, `%{response_code} %{size_download}` = `304 0`, and `-i` still writes the 200 head. Newer: body written. `Last-Modified: garbage`: body written. A 404 with an old `Last-Modified`: no body, exit 0.
  - `-z -date` with `Last-Modified` equal or newer: no body. So both directions are strict and an equal time meets neither.
  - `-r 0-4 -z date` with an old `Last-Modified`: body written (no local check under a range). `-I -z date` with an old `Last-Modified`: head written, exit 0.
  - `--max-filesize 10` with Content-Length 100: exit 63 `curl: (63) Maximum file size exceeded`, no body; with `-i` the head is written; with `-I` exit 63 too; `%{response_code} %{size_download}` = `200 0`. Content-Length 5 with `--max-filesize 5`: body written, exit 0. `--max-filesize 0`: no limit.
  - `--max-filesize 10` with a chunked body of 15, a chunked body of 8+8, or a read-to-close body of 15: `1234567890` / `12345678ab` / `1234567890` written, exit 63 `curl: (63) Exceeded the maximum allowed file size (10) with 10 bytes`, `%{size_download}` 10.
  - `-f --max-filesize 10` with a 404 and Content-Length 100: exit 22, so `-f` is checked first. `-C 100 --max-filesize 50` with a 206 and Content-Length 5: exit 0, so the limit is compared with the Content-Length alone.
- Decisions (ADR-0041): no `Range` with a request body. `Last-Modified` is read in RFC 9110's three HTTP-date forms. The body limit counts encoded bytes with `--compressed` (not measured). A discarded body (3xx under `-L`, a retried 401) is not held to the body limit, but its Content-Length is. An unmet `-z` reports `ResponseCode` 304 with `TimeConditionUnmet` set.
- Follow-up filed: BL-300 (curl's `Content-Range` for `-r` with a request body) and BL-301 (`ITransferContext.MaxFileSize` remark still says only `file://` enforces it; `Curl.Protocol.Abstractions.UnitLibrary` is held by BL-292).
- Gates: `dotnet build -warnaserror` clean, 0 warnings. Fast tests all pass (Curl.Protocol.Http.UnitTests 646). `dotnet format --verify-no-changes` clean. `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary`: 100% line, 100% branch, 305 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. HTTP sends Range for -r/-C and If-(Un)Modified-Since for -z, answers a refused resume with exit 33, skips the body for 304/unmet -z/416, enforces --max-filesize with exit 63, and reports Last-Modified for -R
