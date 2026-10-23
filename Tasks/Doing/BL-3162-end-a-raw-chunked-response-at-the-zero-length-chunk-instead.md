---
id: BL-3162
title: End a --raw chunked response at the zero-length chunk instead of waiting for the server to close
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpResponseBodyFraming.cs, Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs, Curl.Protocol.Http.UnitTests/HttpResponseBodyReaderRawChunkedTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3162 — End a --raw chunked response at the zero-length chunk instead of waiting for the server to close

## Goal

`curl --raw` on a keep-alive chunked response ends at the terminating zero-length chunk and exits 0 instead of hanging until the server closes.

## Context

Stewart reported on 2026-10-22: serve `5\r\nhello\r\n0\r\n\r\n` keep-alive (with `Transfer-Encoding: chunked`), then `curl -s --raw URL`. Upstream prints the raw chunked bytes and exits 0; Curl prints them and then hangs until the server closes. With `--raw` the engine must still find the zero-length chunk and any trailers and end the transfer, passing the bytes through undecoded. Start in `Curl.Protocol.Http.UnitLibrary/HttpResponseBodyFraming.cs` (`Of(headers, raw, ...)`: with `raw` the framing probably reports neither chunked nor a length, so `RunsToClose` is true) and `HttpResponseBodyReader.cs`; `HttpConnectionPersistence.KeepsAlive` takes the same `Raw` flag. Measure curl 8.22.0 with `Record-CurlExchange.ps1` for the bytes written, for trailers and for what a following request on the same connection does, before pinning.

## Acceptance criteria

- [ ] A new test in `Curl.Protocol.Http.UnitTests/HttpResponseBodyReaderRawChunkedTests.cs` feeds `5\r\nhello\r\n0\r\n\r\n` from a connection that never closes: with `Raw` set, the read completes without waiting for close (no real wait) and the output bytes equal the server's bytes exactly.
- [ ] A test with trailers (`0\r\nX-T: v\r\n\r\n`) passes the trailers through undecoded and ends at the blank line.
- [ ] A test for a chunked body truncated by close before the zero chunk under `--raw` pins the exit code curl 8.22.0 gives, with the measurement noted in the test.
- [ ] A handler-level test through `HttpProtocolHandler` shows `CurlExitCode.Ok` and the raw bytes for the keep-alive case, and that a non-`--raw` chunked response still decodes as before.
- [ ] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean and `dotnet test Curl.Protocol.Http.UnitTests --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 289d73d8 ff5392bf d228a04e 11884a2b 10ca7151.
