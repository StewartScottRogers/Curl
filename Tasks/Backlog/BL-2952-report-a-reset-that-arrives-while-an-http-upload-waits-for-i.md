---
id: BL-2952
title: Report a reset that arrives while an HTTP upload waits for its next body piece as curl's exit 56 Recv failure, not 55
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpRequestBodyWriter.cs, Curl.Protocol.Http.UnitLibrary/HttpConnectionSend.cs, Curl.Protocol.Http.UnitTests/HttpRequestBodyWriterWaitingPieceResetTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2952 — Report a reset that arrives while an HTTP upload waits for its next body piece as curl's exit 56 Recv failure, not 55

## Goal

When the server answers and resets the connection while an HTTP/1.x upload is waiting for its next body piece (a `--limit-rate` wait, or a slow upload source), Curl ends with exit 56 `Recv failure: Connection was reset`, as curl 8.22.0 does, instead of exit 55 `Send failure: Connection was reset` from the next write.

## Context

Found by BL-2938 (AF-0300). BL-2938 holds a `-T`/`-F` upload at `--limit-rate` with `Curl.Console`'s `RateLimitedUploadStream` (its work waits in the lane stash until this task is Done). With it, the finding's reproduction (`--form f=@f.txt --limit-rate 10 http://127.0.0.1:PORT/` against `Record-CurlExchange.ps1`, which answers after a one-second pause in the request and closes) now throttles the body as curl does (curl 233 bytes sent, Curl 253, timing-dependent), but ends `curl: (55) Send failure: Connection was reset` where curl 8.22.0 ends `curl: (56) Recv failure: Connection was reset`.

Why curl differs: while the send is held (rate limit, or no upload data ready), curl's transfer loop polls the socket for reading only, and `Curl_sendrecv` reads before it writes, so the peer's reset is met by `recv` (exit 56). A write that fails at full speed stays exit 55 (ADR-0040, FR-085, BL-174): do not change that case.

Where to start: `HttpRequestBodyWriter` reads a `StreamBody` piece by piece (`ReadAsync(body.Content, ...)`, line ~398) and writes each with `HttpConnectionSend.WriteAsync`. One approach: while waiting for a body read that does not complete at once, race a read of the connection (as `EarlyResponseWatch` / `HttpContinueWaitConnection` already races pieces against a response), and a reset seen there ends the transfer with exit 56 and `CurlSocketErrorText`'s `Recv failure: ` words. Unit-test it with a fake `IConnection` and an upload stream that completes a read late, so no network is needed.

## Acceptance criteria

- [ ] A unit test in `Curl.Protocol.Http.UnitTests` shows a reset arriving while the body writer waits on its upload source ends the transfer with exit 56 and `Recv failure: Connection was reset` (Windows wording; its own off-Windows test where the words differ).
- [ ] The existing full-speed send-reset tests (`ExecuteAsync_SocketErrorFailsASend_FailsWithExit55AndTheWinsockWords` and its siblings) still pass unchanged.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-21: Created.
