---
id: BL-1040
title: Write curl's Basic and Bearer authentication problem -v lines on an HTTP 401
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-30
completed: 2026-10-01
---
# BL-1040 — Write curl's Basic and Bearer authentication problem -v lines on an HTTP 401

## Goal

`curl -v -u u:p http://...` (Basic picked) or `--oauth2-bearer tok` answered by a 401 writes `* Basic authentication problem, ignoring.` (or `Bearer`) just before each `WWW-Authenticate` header, once per comma-separated challenge offering the picked scheme, as curl 8.21.0 does.

## Context

- BL-953 (ADR-0278) measured this on a WebSocket upgrade with curl 8.21.0 and wrote it there (`Curl.Protocol.Ws.UnitLibrary/WsAuthProblemLines.cs`); the HTTP handler writes neither line today. It is libcurl's `Curl_http_input_auth`: on a 401, a Basic (or Bearer) challenge when that scheme is the picked one (the one scheme allowed, with a user or token) reports the problem.
- Measured on the WebSocket upgrade: the line is written even when `-H "Authorization: ..."` replaced the value; not on a 403; not when the 401 offers only Digest; before each of two Basic headers.
- The HTTP handler already places lines before a challenge header through `DefersFrom`/`IsAuthChallenge` (`HttpNtlmInfoLines`, BL-848); follow that. Re-measure plain HTTP with `Record-CurlExchange.ps1` before pinning, including whether curl retries (it should not, with `authproblem` set).

## Acceptance criteria

- [x] A `Curl.Protocol.Http.UnitTests` event test pins `Basic authentication problem, ignoring.` between a 401's status line and its `WWW-Authenticate: Basic` header for `-u u:p`.
- [x] A test pins the `Bearer authentication problem, ignoring.` line for `--oauth2-bearer tok` against `WWW-Authenticate: Bearer`.
- [x] `--anyauth` and a 403 write neither line.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Filed by BL-953.

Measured with curl 8.21.0 (Windows, Schannel) and Record-CurlExchange.ps1 on 2026-10-01:

- `-v -u u:p` against `401` + `WWW-Authenticate: Basic realm="x"`: `* Basic authentication problem, ignoring.` between the status line and the header; one request only (no retry).
- `--oauth2-bearer tok` against `WWW-Authenticate: Bearer realm="x", basic x`: one `* Bearer authentication problem, ignoring.` (only the Bearer challenge counts).
- `-H "Authorization: Foo" -u u:p` against `Basic realm="x", Basic y` then `www-authenticate: basic z`: two lines before the first header, one before the second (any case).
- `403` with the same challenge: no line. `--anyauth`: no line on the first 401 (nothing picked yet); its retry, sent with Basic picked, gets the line before the second 401's challenge.
- Proxy: `-x ... -U u:p` against `407` + `Proxy-Authenticate: Basic realm="x", Basic y`: two lines before that header. Same libcurl code (`Curl_http_input_auth`), so it is included here at no extra cost.

Design: `HttpAuthProblemLines.LinesBefore` decides from the value the request sent (Basic or Bearer means curl picked that scheme, as `authp->picked`), the status (401 origin, 407 proxy) and the header name; the handler writes the lines from its `HeaderReceived` callback, which runs just before each acted-on header's `<` lines. Basing it on the sent value rather than the allowed schemes is what makes `--anyauth` right on both legs. No ADR: no choice beyond what curl measured.

`dotnet format --verify-no-changes` flags end-of-line markers in the untouched `HttpProtocolHandlerTests.AuthenticationHandshake.cs` (pre-existing, not this task's).

## Log

- 2026-09-30: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. curl -v writes Basic/Bearer authentication problem lines before a refusing 401 or 407 challenge
