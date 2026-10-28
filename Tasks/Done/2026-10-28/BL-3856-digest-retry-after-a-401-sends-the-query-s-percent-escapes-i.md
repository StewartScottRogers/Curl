---
id: BL-3856
title: Digest retry after a 401 sends the query's percent-escapes in upper case as curl 8.22.0 does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpUrlText.cs, Curl.Protocol.Http.UnitLibrary/HttpRequestHeadFormatter.cs, Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitTests/DigestRetryQueryEscapesTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3856 — Digest retry after a 401 sends the query's percent-escapes in upper case as curl 8.22.0 does

## Goal

After a 401 Digest challenge, Curl's retried request sends the query's `%xx` escapes in upper case, in the request line and the Digest `uri=`, as curl 8.22.0 does.

## Context

Found by BL-3583 (CGR-054, ADR-0911). Measured on 2026-10-26 with `Record-CurlExchange.ps1` (two connections: a `401` with `WWW-Authenticate: Digest realm="r", nonce="n"` and `Connection: close`, then `200`) for `curl -sS --digest -u u:p "http://127.0.0.1:P/a%2fb?q=%2f"`:

- curl 8.22.0 (Git for Windows ucrt64): first `GET /a%2Fb?q=%2f`, retry `GET /a%2Fb?q=%2F` with `uri="/a%2Fb?q=%2F"`, `response="986491e1ce8e096a5f36c9a412ac41eb"`.
- Curl after BL-3583: retry `GET /a%2Fb?q=%2f` with `uri="/a%2Fb?q=%2f"`.

The first request keeps the query as typed; only the retry upper-cases it (curl re-reads the URL for the new request). Measure whether Basic, NTLM and a followed redirect behave the same before deciding where the retry's target is built (`HttpProtocolHandler`, `HttpUrlText.OriginTarget`).

## Acceptance criteria

- [x] The command above, through real curl and Curl, sends the same request bytes on both connections; the measurement is in Notes.
- [x] A focused unit test in `Curl.Protocol.Http.UnitTests` fails before the fix and passes after it.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Touches widened to HttpRequestHeadFormatter.cs and HttpProtocolHandler.cs (no other Doing task holds them): the retry plan carries `RereadsUrl`, which upper-cases the whole target in the request line and the Digest uri; `--request-target` is left as given.
- Measured 2026-10-28 with curl 8.22.0: a 302 to /c%2fd?q=%2f followed with -L is requested as /c%2Fd?q=%2F, so redirects upper-case the query too (not changed here). The Digest retry bytes come from the 2026-10-26 measurement above (my re-run failed on response quoting); the unit tests pin them. Basic/NTLM not re-measured: every scheme answering a 401 shares the flag.
- Quality run for Curl.Protocol.Http.UnitLibrary: 0 failing members. Fast tests green; tests are platform-neutral.

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 4c933e76 ad30c61c 779790c0.
- 2026-10-28: Doing -> Done. Digest retry upper-cases the query escapes
