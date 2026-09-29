---
id: BL-956
title: Print curl's Issue another request line before each authentication retry
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-844]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-956 — Print curl's Issue another request line before each authentication retry

## Goal

Under `-v`, every request `HttpProtocolHandler` sends again in answer to a 401 or 407 is preceded by curl 8.21.0's `* Issue another request to this URL: '<url>'` line, right after the challenge's `Connection #N to host ... left intact`, and followed by `Reusing existing http: connection with host ...` when the connection is reused.

## Context

- Measured 2026-09-29 (ADR-0232): `--anyauth -u : -v` against `401` + `WWW-Authenticate: Negotiate` writes, after the first 401's head, `* Connection #0 to host 127.0.0.1:48844 left intact`, `* Issue another request to this URL: 'http://127.0.0.1:48844/'`, `* Reusing existing http: connection with host 127.0.0.1`, then the second request's lines. Curl writes neither the `Issue another request` line nor (in the handler's events) the reuse line before an authentication retry, for any scheme; today only a request resent after its connection died writes the first (`HttpProtocolHandler.ReportConnectionEnd`, `HttpConnectionInfoLines.IssueAnotherRequest`).
- `HttpProtocolHandlerTests.NegotiateAnyAuth.cs` (BL-844) pins the `-v` lines without these two; add them there once written. Measure Digest and NTLM retries with `Record-CurlExchange.ps1` (`-HoldOpenMilliseconds` keeps the connection for reuse) before pinning them.
- Related: BL-907 does the same line for followed `-L` redirects in `Curl.Core.UnitLibrary`.

## Acceptance criteria

- [ ] `HttpProtocolHandlerTests` pins `* Issue another request to this URL: '...'` before the retry of a Digest 401, an NTLM 401 and the `--anyauth` Negotiate 401 of BL-844, in the measured order.
- [ ] The reuse line, however it is written for `-v` (handler info line or `ReportConnectionReused` rendered by the console), appears in the measured place for a retry on the same connection.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http.UnitLibrary`.

## Notes

## Log

- 2026-09-29: Created.
