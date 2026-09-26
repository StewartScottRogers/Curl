---
id: BL-177
title: Decode --compressed HTTP bodies with gzip, deflate and br
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-173, BL-154]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-177 — Decode --compressed HTTP bodies with gzip, deflate and br

## Goal

With `Compressed` set the handler sends the BL-154 ADR's Accept-Encoding and decodes gzip, deflate (zlib and raw) and br, including stacked encodings.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item H9. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-154 ADR: `Accept-Encoding: deflate, gzip, br` (no zstd until a hand-written decoder exists). BCL: `GZipStream`, `ZLibStream`, `DeflateStream`, `BrotliStream`.
- Measured: bad gzip gives exit 61 `curl: (61) Error while processing content unencoding: incorrect header check`.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] The request carries `Accept-Encoding: deflate, gzip, br` exactly when `Compressed` is set.
- [x] gzip, zlib-wrapped deflate, raw deflate, br and a stacked `gzip, br` encoding each decode in a test.
- [x] Corrupt gzip returns `CurlExitCode.BadContentEncoding` (61) with the measured message.
- [x] Without `Compressed`, an encoded body is written untouched.
- [x] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes` (added by BL-169), never a socket; every parser test also runs with 1-byte chunks.

## Notes

- Plan item: H9 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- `touches` gained `Documentation/Planning/Decisions` for ADR-0027 and its index row. No task in Doing named it (BL-072, BL-194, BL-205, BL-218, BL-221 checked).
- **Delivered directly, not through the architect and implementer subagents.** The change is one library and its tests, and the seams already existed (`HttpRequestHeadFormatter`, `HttpResponseBodyReader`). The code-reviewer subagent reviewed the diff. Its findings were fixed: decompressors are now disposed; a followed redirect's body is not decoded; an unknown coding waits for the first body byte.
- **Design.** `HttpContentDecoder.For(headers)` reads every Content-Encoding header. It builds one `HttpContentCodingDecoder` per coding, last applied first. `HttpResponseBodyReader` passes each encoded write through it when `decodeContent` is set. The handler sets `decodeContent` for `Compressed && !Raw`, except for a 3xx body `-L` discards. The BCL streams are pull-based. `HttpContentInput` feeds them each received piece, and they are read until they return 0. A read of 0 does not end a BCL stream for good, so the next piece carries on. Tests feed every coding in 1-byte pieces to hold that in place.
- **Measured.** curl 8.21.0 mingw `/mingw64/bin/curl`, `Record-CurlExchange.ps1 -Port 18177 -CurlArgs '-s','-S','--compressed','http://127.0.0.1:18177/'`. Each response was `HTTP/1.1 200 OK\r\nContent-Encoding: <c>\r\nContent-Length: <n>\r\n\r\n<body>`, with `hello` encoded by the BCL:
  - Request: `GET / HTTP/1.1\r\nHost: 127.0.0.1:18177\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nAccept-Encoding: deflate, gzip, br, zstd\r\n\r\n`. Curl drops `zstd` (ADR-0020). With `-e http://r/ -H "X-A: 1"`, Accept-Encoding comes after Accept and before Referer and `X-A`. `-H "Accept-Encoding: gzip"` replaces curl's own. `--raw --compressed` still sends Accept-Encoding.
  - gzip `1F8B080000000000000ACB48CDC9C9070086A6103605000000`, zlib `789CCB48CDC9C90700062C0215`, raw deflate `CB48CDC9C90700`, br `0B028068656C6C6F03`, and `gzip, br` `0B0C801F8B080000000000000ACB48CDC9C9070086A610360500000003`: each writes `hello`, exit 0. So do `x-gzip`, `GZIP`, zlib data labelled `gzip`, and two headers `gzip` + `br`. `identity` passes through.
  - Without `--compressed`, and with `--raw --compressed`: the 25 gzip bytes are written untouched.
  - `-o NUL -w %{size_download}`: `25`, the encoded size.
  - Corrupt: gzip `000102030405060708090A0B` gives `curl: (61) Error while processing content unencoding: incorrect header check`. gzip `1F8B07000000` gives `…: unknown compression method`. deflate `FFFFFFFF`, and gzip data labelled deflate, give `…: invalid block type`. br `FFFFFFFF` gives `curl: (61) Unrecognized or bad HTTP Content or Transfer-Encoding`. `Content-Encoding: compress` gives `curl: (61) Unrecognized content encoding type`.
  - The first 15 bytes of the gzip body alone write `hell` with exit 0: truncation is no error.
  - `Content-Encoding: foo` with `Content-Length: 0` exits 0, and so does a 304 with it.
  - A 302 with `Content-Encoding: foo` and body `AB`, under `-L --max-redirs 1 --compressed`, ends with `curl: (47) Maximum (1) redirects followed`, not 61, so a followed redirect's body is not decoded.
  - Bytes after the end of the stream (zlib, br, gzip + `41 42`) write `hello`, then `curl: (23) Failed writing received data to disk/application`. Not done here: filed as BL-272.
- **Decision (ADR-0027, decided by Claude under Stewart's delegation).** The BCL does not expose zlib's error text. Curl's own code checks the gzip and zlib headers so that `incorrect header check` and `unknown compression method` match. Any other corrupt data gives curl's generic exit 61 text. For deflate, that differs from the measured `invalid block type`.
- **Default taken.** A failed output write under decoding reports the encoded piece's size as `passed` in the exit 23 message. Curl reports its decoded write size, which was not measured.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. --compressed sends Accept-Encoding: deflate, gzip, br and decodes gzip, zlib and raw deflate, br and stacked codings; corrupt bodies exit 61 as measured
