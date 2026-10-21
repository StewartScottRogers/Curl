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
completed: 2026-10-21
---
# BL-2915 — Send a custom method with a space whole as the HTTP/2 and HTTP/3 :method, as curl 8.22.0 does (#19543)

## Goal

A request whose `-X` method holds a space (`curl --http2 -X "IN SANE" https://host/path`) sends `:method: IN SANE` and `:path: /path` over HTTP/2 and HTTP/3, as curl 8.22.0 does, rather than `:method: IN` and `:path: SANE /path`.

## Context

Found by BL-2893. curl 8.22.0 `tests/unit/unit2603.c` TEST7a/TEST7b (curl issue #19543): `Curl_h1_req_parse_read` takes the custom method, and when the request line starts with it, the whole custom method becomes the method and the rest the target. Curl's `Curl.Protocol.Http.UnitLibrary/Http2RequestHeaders.cs` `Of` splits the request line on every space (`lines[0].Split(' ')`), so a method with a space comes out as `:method IN` and `:path SANE`. Measure real curl first (an HTTP/2 loopback exchange with `-X "IN SANE"`, through `Record-CurlExchange.ps1` or the HTTP/2 conformance server) and pin what it sends. The fix most likely passes the custom method (or splits the request line at the last space, before `HTTP/1.1`, and at the method's length) into `Http2RequestHeaders.Of` from `Http2StreamConnection` and `Http3StreamConnection`; if those callers must change, add them to `touches` under the lane rules.

## Acceptance criteria

- [x] A test in `Curl.Protocol.Http.UnitTests/Http2RequestHeadersTests.cs` shows a head `IN SANE /path HTTP/1.1` with custom method `IN SANE` gives `:method` `IN SANE` and `:path` `/path`, and the existing tests still pass.
- [x] What real curl 8.22.0 sends for `--http2 -X "IN SANE"` is recorded under Notes and matches what Curl sends.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Curl.Protocol.Http.UnitLibrary` keeps 100% line and branch coverage and complexity at most 10.

## Notes

- Measured 2026-10-21: the only HTTP/2-capable curl on this machine is curl.se's 8.18.0 nghttp2 build (WinGet); the installed 8.22.0 (Schannel, Git) has no HTTP/2. `curl -v --http2-prior-knowledge -X "IN SANE" http://127.0.0.1:<port>/path` sends `[:method: IN SANE]`, `[:scheme: http]`, `[:authority: 127.0.0.1:<port>]`, `[:path: /path]`, `[user-agent: curl/8.18.0]`, `[accept: */*]` and reports `> IN SANE /path HTTP/2`. curl 8.22.0's unit2603 TEST7a/TEST7b pins the same split, so this is what 8.22.0 sends; Curl now matches.
- Fix: `Http2RequestHeaders.Of` takes the target as the request line's last word before the version (a target never holds a space) and the method as everything before it, so the callers (`Http2StreamConnection`, `Http3StreamConnection`) did not change and `touches` stayed as filed.
- Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary: 0 failing members. Build -warnaserror clean; fast tests green in all 43 test projects.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 60653e23 899ec023 10a95a96.
- 2026-10-21: Doing -> Done. A custom method with a space (-X 'IN SANE') is sent whole as the HTTP/2 and HTTP/3 :method, with the target as :path
