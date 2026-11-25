---
id: BL-5952
title: Write curl's abort upload -v line after a 417 that arrived while the body waited for 100 Continue
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.UploadReports.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.Retry.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpConnectionInfoLines.cs, Curl.Original.Protocol.Http.UnitTests/HttpProtocolHandlerTests.RedirectLimitVerbose.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5952 — Write curl's abort upload -v line after a 417 that arrived while the body waited for 100 Continue

## Goal

A 417 that arrives while the request body waits for `100 Continue` is followed in `-v` by the same `*` lines, in the same order, as curl 8.22.0, `abort upload` included.

## Context

Found by BL-5944. `Record-CurlExchange.ps1 -HoldOpenMilliseconds 1500` for `curl -sS -v --max-redirs 0 -d x=1 -H "Expect: 100-continue" --expect100-timeout 5 http://127.0.0.1:PORT/` against a 417 with `Content-Length: 4`: curl 8.22.0 writes `* Got HTTP failure 417 while waiting for a 100`, `* abort upload`, `* Ignoring the response-body`; Curl omits `* abort upload`. Measure the case without `--max-redirs` too. See `ReportUploadAbandoned` in `HttpProtocolHandler.UploadReports.cs`, whose summary says nothing follows such a 417 (BL-1446 Notes).

## Acceptance criteria

- [ ] For that exchange, with and without `--max-redirs 0`, Curl's `-v` lines from the 417's status line to the empty line are curl 8.22.0's, pinned by a unit test.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 51cdfcb2 3d836d5e.
