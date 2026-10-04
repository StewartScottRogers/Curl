---
id: BL-1430
title: Write curl's Proxy-Connection keep-alive/close and Got HTTP failure 417 -v lines
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: FR-090
created: 2026-10-04
completed:
---
# BL-1430 — Write curl's Proxy-Connection keep-alive/close and Got HTTP failure 417 -v lines

## Goal

Curl writes curl 8.21.0's `-v` lines `HTTP/1.0 proxy connection set to keep alive` and `HTTP/1.1 proxy connection set close` for a `Proxy-Connection` response header, and `Got HTTP failure 417 while waiting for a 100` / `Got HTTP failure 417 while sending data` before it resends a request a 417 refused, where and when curl writes them.

## Context

- Upstream (tag `curl-8_21_0`), `lib/http.c` lines 3428-3452 (`http_header_p`): a `Proxy-Connection:` header, when the connection goes through an HTTP proxy (`conn->http_proxy.peer`), writes `infof("HTTP/1.0 proxy connection set to keep alive")` when the response is HTTP/1.0 and the value says `keep-alive`, and `infof("HTTP/1.1 proxy connection set close")` when the response is HTTP/1.1 and the value says `close`. `HD_IS_AND_SAYS` compares the header name and the value case-insensitively. No line is written without a proxy, or for any other version/value pair.
- Upstream `lib/http.c` lines 4046-4067: a 417 that arrives before the whole body is sent, while curl's `Expect: 100-continue` is in use, writes `Got HTTP failure 417 while waiting for a 100` when nothing of the body was sent yet and the wait was still running, else `Got HTTP failure 417 while sending data`; then curl drops `Expect` and resends.
- Curl today: `Curl.Protocol.Http.UnitLibrary/HttpResponseHeadReader.cs` `InfoLineBefore` already writes the analogous `HTTP/1.0 connection set to keep alive` line (`HttpConnectionInfoLines.Http10KeepAlive`) before a header line; nothing writes the two proxy lines (no `Proxy-Connection` handling exists in the library). The 417 resend is built (`HttpProtocolHandler.RetriesWithoutExpect`, BL-260, BL-319, BL-396) but writes neither 417 line.
- Measure before pinning the placement: `Record-CurlExchange.ps1` with `-x http://127.0.0.1:<port>` and a canned `HTTP/1.0 200 OK\r\nProxy-Connection: keep-alive\r\n...` (and an `HTTP/1.1` one with `Proxy-Connection: close`), and for the 417 a `-H "Expect: 100-continue" -d x` request answered `HTTP/1.1 417 Expectation Failed` (wait still running), plus `-d @<file of 2 MiB>` with `-ResponseDelayMilliseconds 1500` (body already being sent). Record which line each `*` line comes before in `stderr.txt` and put it in this task's Notes.

## Acceptance criteria

- [ ] `HttpConnectionInfoLines` holds the four texts byte for byte as above.
- [ ] Tests in `Curl.Protocol.Http.UnitTests` pin, through an HTTP proxy: an HTTP/1.0 response with `Proxy-Connection: Keep-Alive` writes `* HTTP/1.0 proxy connection set to keep alive` at the measured position; an HTTP/1.1 response with `Proxy-Connection: close` writes `* HTTP/1.1 proxy connection set close`; and an HTTP/1.1 `keep-alive`, an HTTP/1.0 `close`, and either header on a direct (no proxy) transfer write neither line.
- [ ] Tests pin `Got HTTP failure 417 while waiting for a 100` for a 417 received while the 100-continue wait runs with no body byte sent, and `Got HTTP failure 417 while sending data` for one received once the body is being sent, each at the measured position, followed by the resend that already happens today; a 417 that does not lead to a resend (under `-f`, or one that closes the connection) writes neither line unless the measurement shows curl writes it there too.
- [ ] Nothing else in the existing `-v` tests changes; `dotnet build Curl.Protocol.Http.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Http.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-04: Created.
- 2026-10-04: Backlog -> Doing.
