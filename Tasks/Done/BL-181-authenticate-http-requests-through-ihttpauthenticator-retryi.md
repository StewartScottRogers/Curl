---
id: BL-181
title: Authenticate HTTP requests through IHttpAuthenticator, retrying a 401 once
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-173, BL-161]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Documentation]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-181 — Authenticate HTTP requests through IHttpAuthenticator, retrying a 401 once

## Goal

The handler asks `IHttpAuthenticator` for an Authorization value, sends Basic up front, and on a 401 challenge retries once on the same connection when it is keep-alive, otherwise on a new one.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item H13. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Measured `-u u:p`: `Authorization: Basic dTpw` is sent immediately after `Host` and before `User-Agent`.
- Challenges are the `WWW-Authenticate` values; `QueueConnector` scripts the reconnect case.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] Basic is sent on the first request in the measured position.
- [x] A 401 with a challenge retries once; keep-alive reuses the connection, `Connection: close` reconnects (tests for both).
- [x] An authentication failure returns the exit code measured on curl 8.21.0 (for example `CurlExitCode.AuthError` 94 or the 401 as a success) with its message.
- [x] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes` (added by BL-169), never a socket; every parser test also runs with 1-byte chunks.

## Notes

- Plan item: H13 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Added `Documentation` to `touches` for ADR-0032 and its index line; no task in Doing names it.
- Measured with `/mingw64/bin/curl` 8.21.0 against a Python loopback server on 127.0.0.1:18183 that serves scripted responses on one keep-alive connection and closes after a response carrying `Connection: close` (`Record-CurlExchange.ps1` closes every connection, so it cannot show reuse). `-w` was `
%{http_code} %{num_connects} %{size_request} %{size_header} %{size_download}
`. Responses: challenge `HTTP/1.1 401 Unauthorized
WWW-Authenticate: Digest realm="r", nonce="n"
Content-Length: 4

nope`, then `HTTP/1.1 200 OK
Content-Length: 2

ok`.
  - `curl -s -u u:p URL` -> request `GET /a HTTP/1.1
Host: 127.0.0.1:18183
Authorization: Basic dTpw
User-Agent: curl/8.21.0
Accept: */*

`; `ok`, `200 1 107 38 2`, exit 0.
  - `curl -s -i -u u:p URL` against a Basic 401 -> one request only; the 401 head and `nope`, `401 1 107 83 4`, exit 0.
  - `curl -sS -f -u u:p URL` against a Basic 401 -> `curl: (22) The requested URL returned error: 401`, exit 22.
  - `curl -s -i --digest -u u:p URL`, keep-alive 401 then 200 -> both requests on connection 1, the second with `Authorization: Digest username="u",realm="r",nonce="n",uri="/a",response="544c035f0f40d9ebf0295157d8041f8c"` after `Host`; both heads, body `ok`, `200 1 269 133 2`, exit 0.
  - The same with `Connection: close` on the 401 -> retry on connection 2; `200 2 269 152 2`, exit 0.
  - `--digest`, 401 then a second 401 (`Content-Length: 5`, `nope2`) -> no third request; both heads, `nope2`, `401 1 269 190 5`, exit 0. With `-sS -f`: `curl: (22) The requested URL returned error: 401` after both heads, `401 1 269 190 0`, exit 22.
  - `--digest` against a 401 with no `WWW-Authenticate` -> no retry; `nope`, `401 1 80 48 4`, exit 0.
  - `--anyauth -u u:p -d hello` against a Basic 401 -> the body is sent with both requests; `ok`, `200 1 335 121 2`, exit 0.
- Exit code for an authentication failure: measured exit 0 with the 401 as the result (exit 22 under `-f`), never 94. Decision and the unmeasured stream-body case recorded in ADR-0032.
- The run that was cut off by the token limit had written the code and tests; this run re-measured every pinned value (all matched), split `RetryAuthorization` (complexity 12) and replaced the two private records with classes so no compiler-generated member goes uncovered.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. Basic up front, one 401 retry on the same or a new connection, 401 as the result (exit 0, 22 under -f) as measured on curl 8.21.0; ADR-0032
