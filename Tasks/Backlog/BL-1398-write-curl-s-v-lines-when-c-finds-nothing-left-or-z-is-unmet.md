---
id: BL-1398
title: Write curl's -v lines when -C finds nothing left or -z is unmet on an HTTP response, and shut the connection down
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: FR-080
created: 2026-10-03
completed:
---
# BL-1398 — Write curl's -v lines when -C finds nothing left or -z is unmet on an HTTP response, and shut the connection down

## Goal

When an HTTP download ends after the head because `-C` asked for an offset the response's `Content-Length` says is the whole document, or because the response's `Last-Modified` fails `-z`, `-v` shows curl 8.21.0's info lines between the last header line and the empty `< ` line, and the connection is shut down (`* shutting down connection #0`) instead of left intact.

## Context

- Today `Curl.Protocol.Http.UnitLibrary/HttpDownloadConditions.cs` decides these cases (`DecideResume` returns `HttpBodyDelivery.NothingLeftToResume` when the Content-Length equals the resume offset; `DecideTimeCondition` returns `HttpBodyDelivery.TimeConditionUnmet`), and `HttpProtocolHandler.ReadBodyAsync` reads nothing for them, but no info line is reported: none of `The entire document is already downloaded`, `The requested document is not new enough`, `The requested document is not old enough` or `Simulate an HTTP 304 response` exists in the HTTP library (the FTP library has the two `not ... enough` texts in `FtpTransferMessages`).
- curl 8.21.0 (tag `curl-8_21_0`), `lib/http.c` `http_firstwrite`: lines 2683-2697, when resuming a GET with no `Content-Range` and the size equals the resume point, `infof(data, "The entire document is already downloaded")` then `streamclose(conn, "already downloaded")`; lines 2706-2721, when `-z` is set, no range was asked for and `Curl_meets_timecondition` fails, `infof(data, "Simulate an HTTP 304 response")` then `streamclose(conn, "Simulated 304 handling")`. `lib/transfer.c` lines 121-145 (`Curl_meets_timecondition`) write `The requested document is not new enough` (`-z date`) or `The requested document is not old enough` (`-z -date`) first.
- Measured 2026-10-03 with curl 8.21.0 (mingw, Schannel) and `Record-CurlExchange.ps1`:
  - `-sv -C 5` against `HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello`: `< HTTP/1.1 200 OK`, `< Content-Length: 5`, `* The entire document is already downloaded`, `< `, `* shutting down connection #0`; nothing on stdout; exit 0.
  - `-sv -z "Jan 1 2020"` against `HTTP/1.1 200 OK\r\nLast-Modified: Mon, 01 Jan 2001 00:00:00 GMT\r\nContent-Length: 5\r\n\r\nhello`: after `< Content-Length: 5` come `* The requested document is not new enough`, `* Simulate an HTTP 304 response`, `< `, `* shutting down connection #0`; nothing on stdout; exit 0.
  - `-sv -C 5` against a `416` with `Content-Length: 0`: `* setting size while ignoring`, `< `, `* Connection #0 ... left intact` (already built; unchanged by this task).
- A real `304` under `-z` (`TimeConditionUnmet` from a 304 status) writes none of these lines; only the simulated one does.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Http.UnitTests` drives the handler with `ResumeFrom = 5` against the first measured response and pins the `-v` sequence above (the info line after the last header line and before the empty line), nothing written to the output, exit 0, and `shutting down connection #0` rather than `left intact`.
- [ ] A test pins the `-z` (if-modified-since) case above: both info lines in that order, the shutdown line, exit 0, nothing written; a second test with an if-unmodified-since condition and a newer `Last-Modified` pins `The requested document is not old enough` followed by `Simulate an HTTP 304 response`.
- [ ] Tests pin that a real `304` response under `-z`, a resume honoured by `206` with `Content-Range`, and the `416` case write none of the new lines and keep today's connection reuse.
- [ ] `dotnet build Curl.Protocol.Http.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Http.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports no failing member in the code this task changed.

## Notes

- Tests must be platform-neutral: the responses are fixed byte strings and no path is involved.

## Log

- 2026-10-03: Created.
