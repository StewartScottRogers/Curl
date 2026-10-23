---
id: BL-3162
title: End a --raw chunked response at the zero-length chunk instead of waiting for the server to close
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpResponseBodyFraming.cs, Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs, Curl.Protocol.Http.UnitTests/HttpResponseBodyReaderRawChunkedTests.cs, Curl.Protocol.Http.UnitLibrary/HttpExchangeLog.cs, Curl.Protocol.Http.UnitTests/HttpExchangeLogTests.cs, Curl.Protocol.Http.UnitTests/HttpResponseBodyFramingTests.cs, Curl.Protocol.Http.UnitTests/HttpConnectionPersistenceTests.cs, Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.TransferEncoding.cs]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3162 — End a --raw chunked response at the zero-length chunk instead of waiting for the server to close

## Goal

`curl --raw` on a keep-alive chunked response ends at the terminating zero-length chunk and exits 0 instead of hanging until the server closes.

## Context

Stewart reported on 2026-10-22: serve `5\r\nhello\r\n0\r\n\r\n` keep-alive (with `Transfer-Encoding: chunked`), then `curl -s --raw URL`. Upstream prints the raw chunked bytes and exits 0; Curl prints them and then hangs until the server closes. With `--raw` the engine must still find the zero-length chunk and any trailers and end the transfer, passing the bytes through undecoded. Start in `Curl.Protocol.Http.UnitLibrary/HttpResponseBodyFraming.cs` (`Of(headers, raw, ...)`: with `raw` the framing probably reports neither chunked nor a length, so `RunsToClose` is true) and `HttpResponseBodyReader.cs`; `HttpConnectionPersistence.KeepsAlive` takes the same `Raw` flag. Measure curl 8.22.0 with `Record-CurlExchange.ps1` for the bytes written, for trailers and for what a following request on the same connection does, before pinning.

## Acceptance criteria

- [x] A new test in `Curl.Protocol.Http.UnitTests/HttpResponseBodyReaderRawChunkedTests.cs` feeds `5\r\nhello\r\n0\r\n\r\n` from a connection that never closes: with `Raw` set, the read completes without waiting for close (no real wait) and the output bytes equal the server's bytes exactly.
- [x] A test with trailers (`0\r\nX-T: v\r\n\r\n`) passes the trailers through undecoded and ends at the blank line.
- [x] A test for a chunked body truncated by close before the zero chunk under `--raw` pins the exit code curl 8.22.0 gives, with the measurement noted in the test.
- [x] A handler-level test through `HttpProtocolHandler` shows `CurlExitCode.Ok` and the raw bytes for the keep-alive case, and that a non-`--raw` chunked response still decodes as before.
- [x] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean and `dotnet test Curl.Protocol.Http.UnitTests --filter "TestCategory!=Integration"` passes.

## Notes

- Measured curl 8.22.0 (mingw, Schannel) with `Record-CurlExchange.ps1`, `-sS --raw`:
  - `5\r\nhello\r\n0\r\nX-T: v\r\n\r\n`: stdout is exactly those bytes, exit 0.
  - `5\r\nhello\r\n` then close: stdout `5\r\nhello\r\n`, `curl: (18) transfer closed with outstanding read data remaining`, exit 18 (the same message and code as without `--raw`).
  - `...0\r\nX-T: 1\r\n\r\nEXTRA` in one write: stdout includes `EXTRA` (curl writes the whole read raw), `-v` says `Leftovers after chunking: 5 bytes` and `left intact`, so the connection is kept for reuse.
  - With `-D file`, the trailer `X-T: 1` is not written to the header output under `--raw`.
- Plan and change: `HttpResponseBodyFraming` gains `PassesChunks` (raw and Transfer-Encoding lists chunked, with or without `--tr-encoding`), which no longer `RunsToClose`, so `HttpConnectionPersistence.KeepsAlive` now keeps the connection, as curl does. `HttpResponseBodyReader.CopyChunkedAsync` still runs the chunk decoder under `--raw` to find the end but writes each read whole; `TrailerBytes` is empty under `--raw`. The info log says `body framed chunked, passed through undecoded`.
- Touches widened (no other Doing task on origin/work/dark-factory names any Curl.Protocol.Http file): `HttpExchangeLog.cs` and its tests for the framing log line; `HttpResponseBodyFramingTests.cs`, `HttpConnectionPersistenceTests.cs` and `HttpProtocolHandlerTests.TransferEncoding.cs`, whose old expectations pinned the run-to-close behaviour (the `EXTRA` row there replayed with 1-byte reads would now stop before `EXTRA`, as curl would; that case moved to a one-read test in the new file).
- No ADR: every behaviour here is measured curl, not a choice.
- Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary: 0 failing members (after extracting `DecodeReadAsync` to keep `CopyChunkedAsync` under complexity 10). Full fast test run green on Windows; the tests use only scripted connections and byte strings, so they are platform-neutral.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 289d73d8 ff5392bf d228a04e 11884a2b 10ca7151.
- 2026-10-22: Doing -> Done. curl --raw on a chunked response ends at the zero-length chunk and its trailers, exit 0, bytes passed through undecoded
