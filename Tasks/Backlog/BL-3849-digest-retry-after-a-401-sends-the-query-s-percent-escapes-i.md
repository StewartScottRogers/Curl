---
id: BL-3849
title: Digest retry after a 401 sends the query's percent-escapes in upper case as curl 8.22.0 does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpUrlText.cs, Curl.Protocol.Http.UnitTests/DigestRetryQueryEscapesTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3849 — Digest retry after a 401 sends the query's percent-escapes in upper case as curl 8.22.0 does

## Goal

After a 401 Digest challenge, Curl's retried request sends the query's `%xx` escapes in upper case, in the request line and the Digest `uri=`, as curl 8.22.0 does.

## Context

Found by BL-3583 (CGR-054, ADR-0821). Measured on 2026-10-26 with `Record-CurlExchange.ps1` (two connections: a `401` with `WWW-Authenticate: Digest realm="r", nonce="n"` and `Connection: close`, then `200`) for `curl -sS --digest -u u:p "http://127.0.0.1:P/a%2fb?q=%2f"`:

- curl 8.22.0 (Git for Windows ucrt64): first `GET /a%2Fb?q=%2f`, retry `GET /a%2Fb?q=%2F` with `uri="/a%2Fb?q=%2F"`, `response="986491e1ce8e096a5f36c9a412ac41eb"`.
- Curl after BL-3583: retry `GET /a%2Fb?q=%2f` with `uri="/a%2Fb?q=%2f"`.

The first request keeps the query as typed; only the retry upper-cases it (curl re-reads the URL for the new request). Measure whether Basic, NTLM and a followed redirect behave the same before deciding where the retry's target is built (`HttpProtocolHandler`, `HttpUrlText.OriginTarget`).

## Acceptance criteria

- [ ] The command above, through real curl and Curl, sends the same request bytes on both connections; the measurement is in Notes.
- [ ] A focused unit test in `Curl.Protocol.Http.UnitTests` fails before the fix and passes after it.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created.
