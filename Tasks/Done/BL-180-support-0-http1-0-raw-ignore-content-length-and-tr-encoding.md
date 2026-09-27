---
id: BL-180
title: Support -0/--http1.0, --raw, --ignore-content-length and --tr-encoding in the HTTP handler
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-173]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-180 — Support -0/--http1.0, --raw, --ignore-content-length and --tr-encoding in the HTTP handler

## Goal

The handler's request line, headers and body handling match curl 8.21.0 for `Version Http10`, `Raw`, `IgnoreContentLength` and `--tr-encoding`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item H12. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Upstream: https://curl.se/docs/manpage.html#-0, #--raw, #--ignore-content-length, #--tr-encoding (curl 8.21.0).
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] For each of the four options the request bytes and the output are measured on curl 8.21.0 and pinned in a test. (`--tr-encoding`: measured and recorded below; implementing and pinning it moved to BL-308 - see Notes.)
- [x] `Raw` writes chunked and encoded bodies undecoded; `IgnoreContentLength` reads to close.
- [x] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes` (added by BL-169), never a socket; every parser test also runs with 1-byte chunks.

## Notes

- Plan item: H12 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- **Scope split (decided by Claude, unattended run).** `HttpRequestOptions` already carries `Version`, `Raw` and `IgnoreContentLength`, but has no member for `--tr-encoding`, so the handler cannot see it. Adding one means editing `Curl.Protocol.Abstractions.UnitLibrary`, which BL-293 (in Doing) touches. Rather than park all four options behind BL-293, this task delivers the three that need nothing outside its `touches` and files BL-308 (depends on BL-180, touches Abstractions and Http) for `--tr-encoding`, with the measured request bytes in its Context. BL-236 now also depends on BL-308.
- No ADR: every behaviour below is measured curl 8.21.0 behaviour, not a design choice.

### Measurements (curl 8.21.0 mingw `/mingw64/bin/curl`, `Record-CurlExchange.ps1 -Port 18180`, URL `http://127.0.0.1:18180/a`; the server sends the response and closes)

| curl arguments | Response | Request line and headers sent | stdout | exit |
| --- | --- | --- | --- | --- |
| `-0` | `HTTP/1.0 200 OK`, `Content-Length: 5`, `hello` | `GET /a HTTP/1.0`, `Host: 127.0.0.1:18180`, `User-Agent: curl/8.21.0`, `Accept: */*` | `hello` | 0 |
| `-0 -d x=1` | same | `POST /a HTTP/1.0`, the three above, `Content-Length: 3`, `Content-Type: application/x-www-form-urlencoded`, body `x=1` | `hello` | 0 |
| `-0 -H "Transfer-Encoding: chunked" -d x` | `HTTP/1.1 200`, CL 5 | `POST /a HTTP/1.0`, the three, `Transfer-Encoding: chunked`, `Content-Type: application/x-www-form-urlencoded`, body `1\r\nx\r\n0\r\n\r\n` | `hello` | 0 |
| `-0 -T <1048577-byte file>` | CL 5 | `PUT /a HTTP/1.0`, the three, `Content-Length: 1048577` - no `Expect` | `hello` | 0 |
| `-0 -T -` (unknown length) | - | nothing (with `-v`: connected, then `* Chunky upload is not supported by HTTP 1.0`) | empty; stderr `curl: (25) Chunky upload is not supported by HTTP 1.0` | 25 |
| `--raw` | `Transfer-Encoding: chunked`, `5\r\nhello\r\n0\r\n\r\n` | default GET | the chunked bytes as sent (15 bytes) | 0 |
| `--raw -i` | chunked, `...0\r\nX-T: 1\r\n\r\nEXTRA` | default GET | head, then `5\r\nhello\r\n0\r\nX-T: 1\r\n\r\nEXTRA` - read to close, 28 bytes, trailers not split out | 0 |
| `--raw` | `Transfer-Encoding: gzip, chunked`, same chunks | default GET | the chunked bytes (no exit 61) | 0 |
| `--raw` | chunked plus `Content-Length: 3` | default GET | all 15 chunked bytes | 0 |
| `--raw` | `Content-Length: 3`, `hello` | default GET | `hel` | 0 |
| `--raw --compressed` | `Content-Encoding: gzip`, CL 5, `hello` | default GET plus `Accept-Encoding: deflate, gzip, br, zstd` | `hello` (undecoded) | 0 |
| `--ignore-content-length` | `Content-Length: 3`, `Connection: close`, `hello` | default GET | `hello` | 0 |
| `--ignore-content-length -w %{size_download}` | `Content-Length: 9`, `hello` | default GET | `hello5` (no exit 18) | 0 |
| `--ignore-content-length` | chunked plus `Content-Length: 3` | default GET | `hello` (chunked still decoded) | 0 |
| `--ignore-content-length` | `Content-Length: abc`, `hello` | default GET | `hello` (no exit 8) | 0 |
| `--ignore-content-length -I` | `Content-Length: 3` | `HEAD /a HTTP/1.1` | the head only | 0 |
| `--tr-encoding` | CL 5, `hello` | default GET plus `TE: gzip`, `Connection: TE` | `hello` | 0 |
| (none, `-s`) | `Transfer-Encoding: gzip`, `hello` | default GET | empty | 61 |

(`Accept-Encoding` keeps Curl's own value without `zstd`, ADR-0020.) The `-0 -T` rows are pinned through `-X PUT` with a body in `HttpRequestOptions.Body` and `-H "Content-Type:"`, which frame it exactly as `-T` does.

### What changed
- `HttpRequestHeadFormatter` ends the request line in `HTTP/1.0` for `Version Http10`; `HttpRequestFraming` adds no `Expect` over HTTP/1.0 and sets `RefusesUnknownLength` for an unknown-length body with no `-H` asking for chunked; `HttpProtocolHandler` fails that with exit 25 after connecting, before sending.
- New `HttpResponseBodyFraming` decides how a body ends for `--raw` (chunked runs to close undecoded, no coding refused, Content-Length otherwise kept) and `--ignore-content-length` (Content-Length never read; chunked still decoded). `HttpResponseBodyReader` and `HttpConnectionPersistence` both use it, so a body that ran to close never leaves its connection reused. `HttpTransferEncoding.ListsChunked` reads the headers without refusing any coding.
- Tests: `HttpProtocolHandlerTests.TransferEncoding.cs` (handler, 1-byte and whole reads), `HttpResponseBodyFramingTests`, and additions to the framing, formatter, transfer-encoding and persistence tests. `Curl.Protocol.Http.UnitTests`: 687 passed. `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary`: 100% line, 100% branch, 0 failing members.
- Not measured, left as is: `--ignore-content-length` with `--max-filesize` or `-C` (both still read the Content-Length header in `HttpDownloadConditions`), and `-0` through a forward proxy.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. -0 sends HTTP/1.0 (exit 25 for an unknown-length body), --raw writes chunked bodies undecoded to close, --ignore-content-length reads to close; --tr-encoding moved to BL-308
