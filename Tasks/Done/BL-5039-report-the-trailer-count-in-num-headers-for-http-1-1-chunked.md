---
id: BL-5039
title: Report the trailer count in num_headers for HTTP/1.1 chunked responses with trailers
priority: Low
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpChunkedDecoder.cs, Curl.Original.Protocol.Http.UnitTests/HttpProtocolHandlerTests.ChunkedTrailerNumHeaders.cs]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-5039 — Report the trailer count in num_headers for HTTP/1.1 chunked responses with trailers

## Goal

For an HTTP/1.1 chunked response that has trailers, `%{num_headers}` reports the trailer count, as REF does.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip; REF = curl 8.22.0 Schannel, REF2 = curl 8.18.0 OpenSSL), finding STD-044 (Low, Confirmed).

- **Repro:** a raw server on 127.0.0.1:41000 answers with `HTTP/1.1 200 OK\r\nX-A: 1\r\nX-B: 2\r\nTransfer-Encoding: chunked\r\n\r\n2\r\nhi\r\n0\r\nX-T: 1\r\n\r\n` then closes; `curl -s -o NUL -w "%{num_headers}\n" http://127.0.0.1:41000/`
- **Expected (REF and REF2):** `1`; when trailers are present curl reports the trailer count.
- **Actual (PORT):** `3`. Without trailers, PORT and REF agree.
- **Where to start:** the HTTP/1.1 chunked path never sets `TransferReport.TrailerCount`. `HttpChunkedDecoder.TrailerCount` only feeds `HttpResponseBodyReader.HeadersStored`, and `HttpProtocolHandler.cs` (about line 3699) sets `TrailerCount` for HTTP/2 only. `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs:147` already prefers `TrailerCount` when it is above 0 (so it needs no change). `HttpResponseBodyReader.cs` may need to expose the decoder's count; add it to `touches` if so.

## Acceptance criteria

- [x] A new test file `Curl.Original.Protocol.Http.UnitTests/HttpProtocolHandlerTests.ChunkedTrailerNumHeaders.cs` (modelled on `Http2TrailerNumHeadersTests.cs`) runs the response above and asserts `TransferReport.TrailerCount` is 1 (so `%{num_headers}` renders `1`).
- [x] A chunked response with no trailers still reports its response header count (`2` for `X-A`, `X-B`), and an HTTP/2 trailer response is unchanged.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Done: HttpResponseBodyReader.ChunkedTrailerCount feeds TransferReport.TrailerCount in HttpProtocolHandler.Report (no touches change needed). Quality run: 0 failing members. Test pins head header count 3 (incl. Transfer-Encoding).

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: 3885beaa 9167f6b6 a0b49a29.
- 2026-11-12: Doing -> Done. Chunked trailers now reported in num_headers
