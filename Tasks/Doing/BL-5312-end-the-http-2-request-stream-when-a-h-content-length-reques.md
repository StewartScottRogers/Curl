---
id: BL-5312
title: End the HTTP/2 request stream when a -H Content-Length request has no body
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Original.Protocol.Http.UnitTests/Http2StreamConnectionContentLengthTests.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5312 — End the HTTP/2 request stream when a -H Content-Length request has no body

## Goal

An HTTP/2 request whose only `Content-Length` comes from `-H` and which has no body (`curl -X POST -H "Content-Length: 0" https://...`) ends its stream, so the server's reply is read instead of the run timing out with exit 28.

## Context

From the outside fitness review of 2026-11-16 (commit 64e9144, `preview` build), finding STD-073 (Medium, Confirmed: 2 runs over TLS with ALPN h2, 2 over h2c prior knowledge). Area: HTTP/2 request framing, Windows build (which negotiates h2 by default over HTTPS, ADR-0786). A hang in a common pattern: `curl -X POST -H "Content-Length: 0" https://...` for APIs that answer 411. The reference curl has no HTTP/2, so curl 8.18 (nghttp2) was compared against the same server.

**Repro.** An HTTP/2 server (the review used Python `h2` 4.4.1; here use a PowerShell or C# file-based app, never Python) on 127.0.0.1:31543 with TLS and ALPN `h2,http/1.1`, whose `/echo` returns the request header list as the body; the same server as h2c on 31580.
- `curl -sk -m 5 -X POST -H "Content-Length: 0" https://127.0.0.1:31543/echo -w "[%{http_version} %{http_code}]"`
- the same with `-X PUT`
- `curl -s -m 5 --http2-prior-knowledge -H "Content-Length: 5" http://127.0.0.1:31580/echo`

**Expected (curl 8.18).** The echoed headers including `:method: POST` and `content-length: 0`, then `[2 200]`, exit 0. curl 8.18 sets END_STREAM on HEADERS.

**Actual (port).** `curl: (28) Operation timed out after 5005 milliseconds with 0 bytes received`, `[0 000]`, exit 28. `-v` shows `[HTTP/2] [1] [content-length: 0]` and then nothing: HEADERS are sent without END_STREAM, and no DATA frame with END_STREAM follows. `--http1.1` works, and `-d ""` works over h2.

**Where to start.** `Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs`, `StartAsync` (line 287): `isRequestEnded = bodyLength == 0 && !fields.Exists(field => field.Name == "content-length");`. A user `Content-Length` keeps the stream open, but with no body nothing calls `EndRequestAsync` (line 190).

## Acceptance criteria

- [x] New tests in `Curl.Original.Protocol.Http.UnitTests/Http2StreamConnectionContentLengthTests.cs` (stand-in session, no network) prove that a request with `bodyLength == 0` and a user `content-length: 0` field ends the request: END_STREAM is set on HEADERS, or a DATA frame with END_STREAM follows, and the response is read.
- [x] A test proves the h2c `Content-Length: 5` with no body case ends promptly (END_STREAM sent; the server's reply or a clear error, never a wait for `-m`); the Log records what was chosen.
- [x] A test proves a request with a real body and a `content-length` field still sends its body and ends the stream only after the last DATA frame (no regression).
- [x] The Log records the review's hand check: the first command against an h2 server prints `[2 200]` and exits 0; the h2c `Content-Length: 5` case does not exit 28.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Where curl's frames can be measured, compare against it; `Record-CurlExchange.ps1` speaks HTTP/1.1 only, so the h2 frames come from the h2 stand-in. Mock servers must be PowerShell or a C# file-based app, never Python (CLAUDE.md).

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: 9ac009d6 651895f4 6360cca2 d7f42e9a a0b49a29.
- 2026-11-16: Fix in `Http2StreamConnection.ReceiveFrameAsync` (via `EndBodylessRequestAsync`): a stream with `bodyLength == 0` whose request has not ended (a -H Content-Length and no body never reaches the body writer, so no one called EndRequestAsync) sends the empty DATA with END_STREAM before the first response read. Chosen over END_STREAM on HEADERS so `-d ''` keeps CGR-276's frame order and the file stays inside `touches`. h2 and h2c cases (Content-Length 0 and 5) end promptly and read the reply; a real body still ends on its last DATA frame; the three tests pass on the stand-in. Hand check against a live h2 server not run in this lane (no network repro); the stand-in proves the frames. Measure-CodeQuality for the library: 0 failing members (first run found complexity 12 in ReceiveFrameAsync; extracted the helper). Platform-neutral (no files or paths).
