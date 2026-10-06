---
id: BL-365
title: Match curl 8.21.0 on bytes after the end of a gzip stream in a decoded body
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-315]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-365 — Match curl 8.21.0 on bytes after the end of a gzip stream in a decoded body

## Goal

A decoded gzip body (Content-Encoding under `--compressed`, or Transfer-Encoding under `--tr-encoding`) with bytes after the end of its gzip stream gives the output, exit code and message curl 8.21.0 gives.

## Context

- Found while measuring BL-315 (2026-09-27). curl 8.21.0 (mingw, `Record-CurlExchange.ps1 -Port 18180`) with `-sS --tr-encoding` against `HTTP/1.1 200 OK\r\nTransfer-Encoding: gzip\r\n\r\n` followed by the 25-byte gzip stream of `hello` and then `XYZ`, closing: stdout `hello`, stderr `curl: (23) Failed writing received data to disk/application`, exit 23.
- `HttpContentCodingDecoder` reads with the BCL's `GZipStream`, which treats trailing bytes as a further gzip member and so fails differently (probably exit 61 `Unrecognized or bad HTTP Content or Transfer-Encoding`; not yet checked).
- Still to measure: the same body as `Content-Encoding: gzip` with `--compressed`, trailing bytes after a `deflate` (zlib) stream, and a second complete gzip member after the first.

## Acceptance criteria

- [x] The case above and each case still to measure are measured on curl 8.21.0, recorded in Notes, and pinned in `Curl.Protocol.Http.UnitTests` tests with 1-byte reads.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`.

## Notes

- 2026-09-27, measured on curl 8.21.0 (mingw, Schannel) with `Record-CurlExchange.ps1`, `-sS`, each response sent then closed; `{gz}` is the 25-byte gzip of `hello`, `{zlib}` the zlib stream, `{world}` a second gzip member:
  - `--tr-encoding`, `Transfer-Encoding: gzip`, `{gz}XYZ` to the close, and with `Content-Length: 28`: stdout `hello`, exit 23 `Failed writing received data to disk/application`.
  - `--tr-encoding`, gzip + `{world}`; deflate + `{zlib}XYZ`; br + Brotli `hello` + `XYZ`: the same, exit 23 write message.
  - `--tr-encoding`, deflate + raw deflate `hello` + `XYZ`: stdout `hello`, exit 0 (trailing bytes dropped, as BL-281 found for Content-Encoding).
  - `--compressed`, `Content-Encoding: gzip` + `{gz}XYZ` or + `{world}`, `Content-Encoding: deflate` + `{zlib}XYZ`: stdout `hello`, exit 23 write message.
  - Inside chunks (`Transfer-Encoding: gzip, chunked` with `--tr-encoding`, or `Content-Encoding: gzip` + `Transfer-Encoding: chunked` with `--compressed`, XYZ in the same chunk or its own): stdout `hello`, exit 23 but message `Failed reading the chunked-encoded stream`. Corrupt gzip or an unknown coding inside chunks keeps its exit 61 message.
- The decoder already failed trailing bytes (BL-281) and the `--tr-encoding` path reuses it, so only the chunked message differed. `HttpResponseBodyReader.WriteChunkDataAsync` now rewrites the exit 23 `ReceivedDataWriteFailed` from chunk data to the new `HttpTransferMessages.ChunkedStreamReadFailed`; every other failure keeps its message (curl's chunked writer only sets its message when no earlier one was set).
- No ADR: behaviour is measured, not chosen.
- Tests: `ExecuteAsync_DecodedBodyWithBytesAfterItsStream_WritesTheStreamThenFailsWithExit23` (11 rows) and a raw-deflate row in `ExecuteAsync_TransferEncoding_SendsTeAndDecodesTheBody`, each at 1-byte and 65536-byte reads.
- Gates: `dotnet build -warnaserror` clean, format clean, fast tests green (Http 963), `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` 100% line, 100% branch, 0 failing, worst CRAP 10.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. Bytes after a decoded gzip, zlib or Brotli stream give curl 8.21.0's exit 23 and message under --tr-encoding and --compressed, the chunked message inside chunks
