---
id: BL-5036
title: Count --tr-encoding bodies after decoding in size_download
priority: Low
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs, Curl.Original.Protocol.Http.UnitTests/HttpResponseBodyReaderTransferEncodingSizeTests.cs]
requirement: none
created: 2026-11-12
completed:
---
# BL-5036 — Count --tr-encoding bodies after decoding in size_download

## Goal

With `--tr-encoding`, `%{size_download}` counts the bytes after the Transfer-Encoding coding is decoded, as REF does.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip; REF = curl 8.22.0 Schannel, REF2 = curl 8.18.0 OpenSSL), finding STD-041 (Low, Confirmed).

- **Repro:** a raw server on 127.0.0.1:42002 answers `GET /enc/te` with `g = gzip.compress(b"hello world " * 50)` (600 bytes in, 39 out) as `HTTP/1.1 200 OK\r\nTransfer-Encoding: gzip, chunked\r\n\r\n` + `%x\r\n` % len(g) + g + `\r\n0\r\n\r\n`. Then `curl -s --tr-encoding http://127.0.0.1:42002/enc/te -o NUL -w "%{size_download} %{size_header}\n"`
- **Expected (REF):** `600 126`.
- **Actual (PORT):** `39 126`. The decoded body is identical. With Content-Encoding and `--compressed`, both REF and PORT count 39.
- **Where to start:** `Curl.Original.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs`, `CopyAsync`. Its `decodeTransfer` doc says `BytesWritten` "still counts the encoded bytes"; Transfer-Encoding codings should be counted after decoding (and that doc comment updated).

## Acceptance criteria

- [ ] A new test file `Curl.Original.Protocol.Http.UnitTests/HttpResponseBodyReaderTransferEncodingSizeTests.cs` reads the gzip+chunked response above through `HttpResponseBodyReader` and asserts the reported download size is 600 while the decoded body is unchanged.
- [ ] `%{size_header}` is unchanged (126 for the repro response).
- [ ] Regression: with `Content-Encoding: gzip` and `--compressed`, the count stays the encoded 39 bytes; a plain chunked response without a Transfer-Encoding coding counts the dechunked body as before.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: 33862ef7 aa72b337 3e5eaa82 a0b49a29.
