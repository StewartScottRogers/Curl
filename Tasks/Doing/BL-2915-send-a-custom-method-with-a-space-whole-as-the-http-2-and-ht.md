---
id: BL-2915
title: Send a custom method with a space whole as the HTTP/2 and HTTP/3 :method, as curl 8.22.0 does (#19543)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/Http2RequestHeaders.cs, Curl.Protocol.Http.UnitTests/Http2RequestHeadersTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2915 — Send a custom method with a space whole as the HTTP/2 and HTTP/3 :method, as curl 8.22.0 does (#19543)

## Goal

A request whose `-X` method holds a space (`curl --http2 -X "IN SANE" https://host/path`) sends `:method: IN SANE` and `:path: /path` over HTTP/2 and HTTP/3, as curl 8.22.0 does, rather than `:method: IN` and `:path: SANE /path`.

## Context

Found by BL-2893. curl 8.22.0 `tests/unit/unit2603.c` TEST7a/TEST7b (curl issue #19543): `Curl_h1_req_parse_read` takes the custom method, and when the request line starts with it, the whole custom method becomes the method and the rest the target. Curl's `Curl.Protocol.Http.UnitLibrary/Http2RequestHeaders.cs` `Of` splits the request line on every space (`lines[0].Split(' ')`), so a method with a space comes out as `:method IN` and `:path SANE`. Measure real curl first (an HTTP/2 loopback exchange with `-X "IN SANE"`, through `Record-CurlExchange.ps1` or the HTTP/2 conformance server) and pin what it sends. The fix most likely passes the custom method (or splits the request line at the last space, before `HTTP/1.1`, and at the method's length) into `Http2RequestHeaders.Of` from `Http2StreamConnection` and `Http3StreamConnection`; if those callers must change, add them to `touches` under the lane rules.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Http.UnitTests/Http2RequestHeadersTests.cs` shows a head `IN SANE /path HTTP/1.1` with custom method `IN SANE` gives `:method` `IN SANE` and `:path` `/path`, and the existing tests still pass.
- [ ] What real curl 8.22.0 sends for `--http2 -X "IN SANE"` is recorded under Notes and matches what Curl sends.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Curl.Protocol.Http.UnitLibrary` keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 60653e23 899ec023 10a95a96.
