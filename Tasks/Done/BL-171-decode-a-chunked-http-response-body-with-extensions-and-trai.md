---
id: BL-171
title: Decode a chunked HTTP response body with extensions and trailers
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-169]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-171 — Decode a chunked HTTP response body with extensions and trailers

## Goal

The Http library decodes `Transfer-Encoding: chunked` bodies, including chunk extensions and trailers, with curl's exits for malformed chunking.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item H3. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Measured: a chunk size line starting `z` gives exit 56 `curl: (56) chunk hex-length char not a hex digit: 0x7a` (curl 8.21.0).
- Chunked wins over Content-Length when both are present (RFC 9112 6.3).
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] Bad hex returns `CurlExitCode.RecvError` (56) with the measured message (the offending byte in `0x%02x`).
- [x] An overflowing chunk size and a missing CRLF after chunk data are measured on curl 8.21.0 and pinned in tests with their exit and message.
- [x] Chunk extensions are ignored, trailers are read and passed to header output as curl does (measured), and chunked plus Content-Length decodes as chunked.
- [x] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes` (added by BL-169), never a socket; every parser test also runs with 1-byte chunks.

## Notes

- Plan item: H3 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Delivered: `HttpChunkedDecoder` (internal, byte-at-a-time state machine: size digits, size line, data, data end, trailer, trailer CR, complete) and `HttpTransferEncoding.IsChunked` (internal). `HttpResponseBodyReader.CopyAsync` checks Transfer-Encoding, then Content-Length (still validated, exit 8), and decodes chunked when listed; each run of chunk data within one read is one output write; reading stops at the empty line after the trailers. Trailer lines, each re-ended with CRLF, are exposed as `HttpResponseBodyReader.TrailerBytes` for the future handler to append to `-i`/`-D` header output after the body (curl writes them there). New messages in `HttpTransferMessages`.
- Measured 2026-09-26 with `/mingw64/bin/curl` 8.21.0 (Schannel): `Record-CurlExchange.ps1 -Port <p> -Response 'HTTP/1.1 200 OK
Transfer-Encoding: chunked

<body>' -CurlArgs -sS[,-i|-D,-],<url>`. Results (body -> outcome):
  - `z...` -> exit 56 `chunk hex-length char not a hex digit: 0x7a`; empty size line -> `...: 0xd`; ` 5` -> `...: 0x20`. So the byte is `%x`, not `%02x` as criterion 1 guessed; measured text wins.
  - 17 digits (`11111111111111111`, `00000000000000005`) -> exit 56 `chunk hex-length longer than 16`. `FFFFFFFFFFFFFFFF`, `8000000000000000` -> exit 56 `invalid chunk size: 'FFFFFFFFFFFFFFFF'`. `7FFFFFFFFFFFFFFF` + `hello` then close -> `hello`, exit 18.
  - `5
helloXX0...` and `hellox` -> `hello`, exit 56 `Malformed encoding found in chunked-encoding`; `hello
` accepted.
  - Accepted, body `hello`: `5;foo=bar` and `0;x` extensions, `5;ab`, `5 x`, `5	`, LF-only lines everywhere, `A` upper-case hex, a 30000-byte extension, bytes after the final empty line (ignored).
  - Close inside data, before the last chunk, after `0
`, or after a trailer line -> what arrived, exit 18 `transfer closed with outstanding read data remaining`.
  - Trailers with `-i`/`-D -`: written after the body, each line as received plus CRLF (an LF-only line gets CRLF, blanks kept), the final empty line not written, earlier trailers written even when a later one fails. `nocolon` and a folded `  more` continuation -> exit 8 `Header without colon`; `: x` and `  a: b` accepted. CR inside or starting a trailer line -> exit 56 Malformed. Trailer line of 4093 bytes before CRLF accepted, 4094/4095/4096 -> exit 100 `Out of memory in chunked-encoding`; the limit is per line (two 3003-byte lines accepted).
  - Content-Length 2 with chunked, either order, and `Content-Length: 99999999999999999999` with chunked -> chunked wins, `hello`. `Content-Length: abc` with chunked, either order -> exit 8.
  - Transfer-Encoding: `CHUNKED`, `chunked, chunked`, two chunked headers, `identity, chunked`, `IDENTITY, Chunked`, `,chunked,`, tabs, `chunked` then an `identity` header, HTTP/1.0 -> chunked. Empty value and `identity` alone -> read to close, raw bytes. `chunked, gzip|identity|foo` -> exit 61 `A Transfer-Encoding (X) was listed after chunked`; `gzip, chunked`, `gzip`, `foo`, `chunked;q=1`, `chunkedx`, `chunked` then a `foo`/`gzip` header -> exit 61 `Unsolicited Transfer-Encoding (X) found`. `Content-Length: abc` then `TE: foo` -> exit 8; `TE: foo` then `Content-Length: abc` -> exit 61 (headers in order). 204 with `TE: foo` or chunked and `-I` with chunked -> no body, exit 0.
- Decision (Claude, sensible default): trailers are exposed on the reader rather than written to a header stream, because no header-output seam exists yet in this library; the handler task that writes `-i`/`-D` appends `TrailerBytes` after the body.
- Not pinned: the `passed N` size curl reports when the output fails on chunked data (only exit 23 is asserted); the exit when the peer closes mid-way through an over-long trailer line.
- Verification: `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` clean; `dotnet build` clean; `dotnet format --verify-no-changes` clean for both projects; `dotnet test --filter "TestCategory!=Integration"` all green (Curl.Protocol.Http.UnitTests 287 passed, none Integration); `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10. Every chunked case runs with 1-, 7- and 65536-byte reads through `ScriptedConnection`.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. HttpResponseBodyReader decodes chunked bodies with extensions and trailers, with curl 8.21.0's measured exits 8, 18, 56, 61 and 100
