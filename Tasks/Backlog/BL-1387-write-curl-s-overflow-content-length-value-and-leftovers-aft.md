---
id: BL-1387
title: Write curl's 'Overflow Content-Length: value' and 'Leftovers after chunking: N bytes' -v lines, with exit 63 for an overflowing length under --max-filesize
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1398]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: FR-067
created: 2026-10-03
completed:
---
# BL-1387 — Write curl's 'Overflow Content-Length: value' and 'Leftovers after chunking: N bytes' -v lines, with exit 63 for an overflowing length under --max-filesize

## Goal

An HTTP/1.x response whose `Content-Length` is too large for a signed 64-bit number writes curl 8.21.0's `* Overflow Content-Length: value` line before that header line and shuts the connection down, or, under `--max-filesize`, fails at once with exit 63 `Maximum file size exceeded`; and a chunked body followed by more bytes in the same read writes `* Leftovers after chunking: N bytes`.

## Context

- Overflow today: `Curl.Protocol.Http.UnitLibrary/HttpContentLength.cs` (`Find`, `TryParseItem`) returns `null` for a number too large, so the body is read to close with no line, the connection is reported `left intact` or not by the usual rules, and `HttpDownloadConditions.ThrowIfContentLengthExceeds` never fires because the length is `null`.
- curl 8.21.0 (tag `curl-8_21_0`), `lib/http.c` lines 3243-3254: on `STRE_OVERFLOW`, if `data->set.max_filesize` is set, `failf(data, "Maximum file size exceeded")` and `CURLE_FILESIZE_EXCEEDED`; otherwise `streamclose(conn, "overflow content-length")` and `infof(data, "Overflow Content-Length: value")`, and the length stays unknown.
- Leftovers today: `HttpResponseBodyReader.CopyChunkedAsync` stops once `HttpChunkedDecoder.IsComplete` and drops any bytes still in `bytes` without a word. curl 8.21.0 `lib/http_chunks.c` lines 449-456: once the chunked state is `CHUNK_DONE` with bytes left in the write, `infof(data, "Leftovers after chunking: %zu bytes", blen)`.
- Measured 2026-10-03 with curl 8.21.0 (mingw, Schannel) and `Record-CurlExchange.ps1` (one canned response per run):
  - `-sv` against `HTTP/1.1 200 OK\r\nContent-Length: 99999999999999999999\r\n\r\nhello`: `< HTTP/1.1 200 OK`, `* Overflow Content-Length: value`, `< Content-Length: 99999999999999999999`, `< `, `{ [5 bytes data]`, `* shutting down connection #0`; stdout `hello`; exit 0.
  - the same with `--max-filesize 10`: `< HTTP/1.1 200 OK`, `* Maximum file size exceeded`, `* closing connection #0` (the `Content-Length` line is not written); nothing on stdout; exit 63.
  - `-sv` against `HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n5\r\nhello\r\n0\r\n\r\nEXTRA` (all in one write): `< Transfer-Encoding: chunked`, `< `, `{ [20 bytes data]`, `* Leftovers after chunking: 5 bytes`, `* Connection #0 to host 127.0.0.1:PORT left intact`; stdout `hello`; exit 0.
- BL-1398 changes the same handler first; build on it.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Http.UnitTests` pins the first overflow case: the info line between the status line and the `Content-Length` header line, the body read to close and written, `shutting down connection #0`, exit 0.
- [ ] A test pins the `--max-filesize 10` overflow case: exit 63 (`CurlExitCode.FilesizeExceeded`) with message `Maximum file size exceeded`, no `Content-Length` header line in the `-v` output, nothing written.
- [ ] A test feeds the chunked response above in one read and pins `Leftovers after chunking: 5 bytes` after the data line and before the left-intact line, output `hello`, exit 0; a test with the trailing bytes arriving in a later read than the last chunk (nothing left over in the read that completes the body) pins no such line.
- [ ] Existing `HttpContentLengthTests` and chunked-body tests pass unchanged except where they pin a full `-v` sequence for one of these responses.
- [ ] `dotnet build Curl.Protocol.Http.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Http.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports no failing member in the code this task changed.

## Notes

- HTTP/2 and HTTP/3 carry no chunked framing, so the leftovers line is HTTP/1.x only.

## Log

- 2026-10-03: Created.
