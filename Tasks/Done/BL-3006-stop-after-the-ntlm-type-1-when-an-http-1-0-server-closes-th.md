---
id: BL-3006
title: Stop after the NTLM type 1 when an HTTP/1.0 server closes the connection with its type 2 so upstream case 159 passes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.NtlmConnectionClosure.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-3006 — Stop after the NTLM type 1 when an HTTP/1.0 server closes the connection with its type 2 so upstream case 159 passes

## Goal

With `-0 --ntlm`, when the 401 carrying the NTLM type 2 also closes the connection, Curl stops there as curl 8.22.0 does, so upstream case 159 passes.

## Context

Split from BL-3003 (whose case 1100 had a different cause). test159 ("known to fail" upstream, HTTP/1.0, `Connection: close` on the type 2 401) differs at line 7 of `<verify><protocol>`: Curl sends another `GET /159 HTTP/1.0` (the type 3 on a new connection) where curl sends nothing more. curl's `lib/http.c` header handling marks the auth a problem when the connection closes while NTLM is at type 2 (`Connection closure while negotiating auth (HTTP 1.0?)`, `data->state.authproblem = TRUE`), so no retry follows and the 401 ends the transfer. Start in `HttpProtocolHandler.cs` where a 401 retry is planned on a closed connection. Measure real curl's `-v` line with `Record-CurlExchange.ps1` before pinning it. Debug the case with `CURL_UPSTREAM_CASE=159` (see `Curl.Conformance.UnitTests/CLAUDE.md`).

## Acceptance criteria

- [x] Upstream case 159 passes and is listed in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] A focused unit test in `Curl.Protocol.Http.UnitTests` pins that a 401 with an NTLM type 2 that closes the connection is not retried (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured curl 8.22.0 (`Record-CurlExchange.ps1`, `-v -0 -u testuser:testpass --ntlm` against an HTTP/1.0 401 with an NTLM Type 2 and `Connection: close`): it writes `* Connection closure while negotiating auth (HTTP 1.0?)` after the last header and before the empty line, and sends nothing more. Curl now does the same: `RetryWithAuthorizationAsync` plans no retry when a 401 to an NTLM Type 1 carries a Type 2 on a connection `HttpConnectionPersistence.KeepsAlive` says ends. No ADR: this matches curl with no choice left open.
- Measure-CodeQuality.ps1 for Curl.Protocol.Http.UnitLibrary: 0 failing members.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 15c3cbe3 36593e1e 3d836d5e.
- 2026-10-21: Doing -> Done. With -0 --ntlm a 401 carrying the NTLM Type 2 on a closing connection ends the transfer as curl does; upstream case 159 passes
