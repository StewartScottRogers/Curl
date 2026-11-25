---
id: BL-5952
title: Write curl's abort upload -v line after a 417 that arrived while the body waited for 100 Continue
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.UploadReports.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.Retry.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpConnectionInfoLines.cs, Curl.Original.Protocol.Http.UnitTests/HttpProtocolHandlerTests.RedirectLimitVerbose.cs, Curl.Original.Protocol.Http.UnitTests/HttpProtocolHandlerTests.ProxyConnectionAnd417Lines.cs]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5952 — Write curl's abort upload -v line after a 417 that arrived while the body waited for 100 Continue

## Goal

A 417 that arrives while the request body waits for `100 Continue` is followed in `-v` by the same `*` lines, in the same order, as curl 8.22.0, `abort upload` included.

## Context

Found by BL-5944. `Record-CurlExchange.ps1 -HoldOpenMilliseconds 1500` for `curl -sS -v --max-redirs 0 -d x=1 -H "Expect: 100-continue" --expect100-timeout 5 http://127.0.0.1:PORT/` against a 417 with `Content-Length: 4`: curl 8.22.0 writes `* Got HTTP failure 417 while waiting for a 100`, `* abort upload`, `* Ignoring the response-body`; Curl omits `* abort upload`. Measure the case without `--max-redirs` too. See `ReportUploadAbandoned` in `HttpProtocolHandler.UploadReports.cs`, whose summary says nothing follows such a 417 (BL-1446 Notes).

## Acceptance criteria

- [x] For that exchange, with and without `--max-redirs 0`, Curl's `-v` lines from the 417's status line to the empty line are curl 8.22.0's, pinned by a unit test.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured on curl 8.22.0 with `Record-CurlExchange.ps1 -HoldOpenMilliseconds 1500`, with and
  without `--max-redirs 0`, from the status line to the empty line: `< HTTP/1.1 417 Expectation
  Failed`, `< Content-Length: 4`, `* Got HTTP failure 417 while waiting for a 100`,
  `* abort upload`, `* Ignoring the response-body`, `* setting size while ignoring`, `< `. The
  run without the limit did not exit under the recorder (curl found the reused connection dead
  and retried on a new one the recorder does not accept); its lines up to the empty line were
  already recorded. The built Curl writes the same lines for `--max-redirs 0`.
- `HttpConnectionInfoLines.AbortUpload(0)` now gives `abort upload`, as curl words it when no
  body byte went out; `ReportUploadAbandoned` writes it after a 417 during the wait. Removed a
  stray second `<summary>` in `HttpProtocolHandler.UploadReports.cs` that described no member.
- `touches` widened with `HttpProtocolHandlerTests.ProxyConnectionAnd417Lines.cs`, whose test
  pinned the old lines; no task in Doing named it.
- No ADR: matching curl's bytes leaves nothing to decide.
- Measure-CodeQuality for `Curl.Original.Protocol.Http.UnitLibrary`: 0 failing members.
- Filed BL-5956 for the other responses that arrive during the wait (500, closing 417, 200).

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 51cdfcb2 3d836d5e.
- 2026-11-25: Doing -> Done. Curl writes abort upload after a 417 that arrives while the body waits for 100 Continue, with and without --max-redirs 0
