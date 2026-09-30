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
completed:
---
# BL-1040 — Write curl's Basic and Bearer authentication problem -v lines on an HTTP 401

## Goal

`curl -v -u u:p http://...` (Basic picked) or `--oauth2-bearer tok` answered by a 401 writes `* Basic authentication problem, ignoring.` (or `Bearer`) just before each `WWW-Authenticate` header, once per comma-separated challenge offering the picked scheme, as curl 8.21.0 does.

## Context

- BL-953 (ADR-0278) measured this on a WebSocket upgrade with curl 8.21.0 and wrote it there (`Curl.Protocol.Ws.UnitLibrary/WsAuthProblemLines.cs`); the HTTP handler writes neither line today. It is libcurl's `Curl_http_input_auth`: on a 401, a Basic (or Bearer) challenge when that scheme is the picked one (the one scheme allowed, with a user or token) reports the problem.
- Measured on the WebSocket upgrade: the line is written even when `-H "Authorization: ..."` replaced the value; not on a 403; not when the 401 offers only Digest; before each of two Basic headers.
- The HTTP handler already places lines before a challenge header through `DefersFrom`/`IsAuthChallenge` (`HttpNtlmInfoLines`, BL-848); follow that. Re-measure plain HTTP with `Record-CurlExchange.ps1` before pinning, including whether curl retries (it should not, with `authproblem` set).

## Acceptance criteria

- [ ] A `Curl.Protocol.Http.UnitTests` event test pins `Basic authentication problem, ignoring.` between a 401's status line and its `WWW-Authenticate: Basic` header for `-u u:p`.
- [ ] A test pins the `Bearer authentication problem, ignoring.` line for `--oauth2-bearer tok` against `WWW-Authenticate: Bearer`.
- [ ] `--anyauth` and a 403 write neither line.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Filed by BL-953.

## Log

- 2026-09-30: Created.
